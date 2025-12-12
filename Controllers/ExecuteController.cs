using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;
using System.Runtime.Versioning;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
[SupportedOSPlatform("windows")]
public class ExecuteController : ControllerBase {
    private readonly ILogger<ExecuteController> _logger;
    private readonly ILogger<WindowsExecutionServiceExec> _execLogger;
    private readonly ILogger<WindowsExecutionServiceAutoit> _autoitLogger;
    private readonly IEdrService _edrService;
    private readonly ExecutionTrackingService _executionTracking;

    public ExecuteController(ILogger<ExecuteController> logger, 
        ILogger<WindowsExecutionServiceExec> execLogger,
        ILogger<WindowsExecutionServiceAutoit> autoitLogger,
        IEdrService edrService,
        ExecutionTrackingService executionTracking) {
        _logger = logger;
        _execLogger = execLogger;
        _autoitLogger = autoitLogger;
        _edrService = edrService;
        _executionTracking = executionTracking;
    }

    [HttpPost("exec")]
    public async Task<ActionResult<ExecuteFileResponse>> ExecuteFile([FromForm] IFormFile file,
        [FromForm] string? drop_path = null, 
        [FromForm] string? executable_args = null, 
        [FromForm] string? execution_mode = null, 
        [FromForm] int? xor_key = null) 
    {
        try {
            // Create the execution service based on execution_mode parameter
            // This will track all execution & artefacts
            IExecutionService executionService;
            if (execution_mode == "exec") {
                executionService = new WindowsExecutionServiceExec(_execLogger);
            } else if (execution_mode == "autoit") {
                executionService = new WindowsExecutionServiceAutoit(_autoitLogger);
            } else {
                // Use "exec" as default execution mode
                executionService = new WindowsExecutionServiceExec(_execLogger);
                _logger.LogInformation("No execution_mode provided, defaulting to 'exec'");
            }
            
            _executionTracking.SetLastExecutionService(executionService);
            _logger.LogInformation("Using execution type: {ExecutionType}", execution_mode);

            // Validate xor_key parameter
            byte? xorKeyByte = null;
            if (xor_key.HasValue) {
                if (xor_key.Value < 0 || xor_key.Value > 255) {
                    _logger.LogWarning("Invalid xor_key value: {XorKey}. Must be between 0 and 255", xor_key.Value);
                    return BadRequest(new ExecuteFileResponse {
                        Status = "error",
                        Message = $"Invalid xor_key value: {xor_key.Value}. Must be between 0 and 255"
                    });
                }
                xorKeyByte = (byte)xor_key.Value;
                _logger.LogInformation("XOR key provided: {XorKey}", xorKeyByte);
            }

            // Validate file upload
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName)) {
                _logger.LogWarning("Data error: file content size: {Size}, filename: {FileName}",
                    file?.Length ?? 0, file?.FileName ?? "");

                return BadRequest(new ExecuteFileResponse {
                    Status = "error",
                    Message = "Invalid request: filename or file data is missing"
                });
            }

            // Determine path
            var targetPath = string.IsNullOrWhiteSpace(drop_path) ? @"C:\RedEdr\data\" : drop_path;
            if (!targetPath.EndsWith(@"\")) {
                targetPath += @"\";
            }
            var filePath = Path.Combine(targetPath, file.FileName);

            // Get file content
            byte[] fileContent;
            using (var memoryStream = new MemoryStream()) {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Write the file
            _logger.LogInformation("Writing file: {FilePath}", filePath);
            if (!await executionService.WriteMalwareAsync(filePath, fileContent, xorKeyByte)) {
                _logger.LogError("Failed to write file to {FilePath}", filePath);
                return StatusCode(500, new ExecuteFileResponse {
                    Status = "error",
                    Message = "Failed to write file"
                });
            }

            // Start EDR collection
            try {
                var edrStartResult = await _edrService.StartCollectionAsync();
                if (edrStartResult) {
                    _logger.LogInformation("Started EDR collection");
                }
                else {
                    _logger.LogWarning("Failed to start EDR collection");
                }
            }
            catch (Exception edrEx) {
                _logger.LogError(edrEx, "Error starting EDR collection");
            }

            // Start the malware
            var (success, pid, errorMessage) = await executionService.StartProcessAsync(executable_args);
            if (!success) {
                if (errorMessage == "virus") {
                    _logger.LogInformation("Malware execution blocked by antivirus");
                    return Ok(new ExecuteFileResponse {
                        Status = "virus",
                        Pid = pid
                    });
                }

                return StatusCode(500, new ExecuteFileResponse {
                    Status = "error",
                    Message = $"Failed to execute malware: {errorMessage}"
                });
            } else {
                _logger.LogInformation("Malware executed successfully with PID: {Pid}", pid);
            }

            return Ok(new ExecuteFileResponse {
                Status = "ok",
                Pid = pid
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in /api/exec");
            return StatusCode(500, new ExecuteFileResponse {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("kill")]
    public async Task<ActionResult<KillResponse>> KillLastExecution() {
        try {
            _logger.LogInformation("Kill request received");

            // Get the last used execution service
            var executionService = _executionTracking.GetLastExecutionService();
            if (executionService == null) {
                _logger.LogWarning("No execution service found - no execution has been run yet");
                return BadRequest(new KillResponse {
                    Status = "error",
                    Message = "No execution service found - no execution has been run yet"
                });
            }

            var (success, errorMessage) = await executionService.KillLastExecutionAsync();

            if (!success) {
                return StatusCode(500, new KillResponse {
                    Status = "error",
                    Message = errorMessage ?? "Failed to kill last execution"
                });
            }

            return Ok(new KillResponse {
                Status = "ok",
                Message = errorMessage
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in /api/kill");
            return StatusCode(500, new KillResponse {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }
}
