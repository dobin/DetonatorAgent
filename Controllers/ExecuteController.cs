using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase {
    private readonly ILogger<ExecuteController> _logger;
    private readonly IEdrService _edrService;
    private readonly ExecutionTrackingService _executionTracking;
    private readonly IEnumerable<IExecutionService> _executionServices;

    public ExecuteController(ILogger<ExecuteController> logger,
        IEdrService edrService,
        ExecutionTrackingService executionTracking,
        IEnumerable<IExecutionService> executionServices) {
        _logger = logger;
        _edrService = edrService;
        _executionTracking = executionTracking;
        _executionServices = executionServices;
    }

    private static string DefaultDropPath() {
        return OperatingSystem.IsWindows() ? @"C:\Users\Public\Downloads\" : "/tmp/";
    }

    private IExecutionService? ResolveExecutionService(string? executionMode) {
        var mode = string.IsNullOrWhiteSpace(executionMode) ? "exec" : executionMode.ToLowerInvariant();
        return _executionServices.FirstOrDefault(s =>
            string.Equals(s.ExecutionTypeName, mode, StringComparison.OrdinalIgnoreCase));
    }

    [HttpPost("exec")]
    [RequestSizeLimit(104857600)] // 100 MB
    public async Task<ActionResult<ExecuteFileResponse>> ExecuteFile([FromForm] IFormFile file,
        [FromForm] string? drop_path = null, 
        [FromForm] string? executable_args = null, 
        [FromForm] string? execution_mode = null, 
        [FromForm] int? xor_key = null) 
    {
        _logger.LogInformation("Exec: Execute request received for file: {FileName}", file?.FileName ?? "null");

        try {
            // Resolve the execution service from DI by ExecutionTypeName.
            // On Windows the DI container has "exec", "autoit", "clickfix".
            // On Linux only "exec" is registered.
            var executionService = ResolveExecutionService(execution_mode);
            if (executionService == null) {
                var available = string.Join(", ", _executionServices.Select(s => s.ExecutionTypeName));
                _logger.LogWarning("Exec: Unknown execution_mode '{Mode}'. Available: {Available}",
                    execution_mode, available);
                return BadRequest(new ExecuteFileResponse {
                    Status = "error",
                    Message = $"Unknown execution_mode '{execution_mode}'. Available: {available}"
                });
            }

            _logger.LogInformation("Exec: Using execution type: {ExecutionType}", executionService.ExecutionTypeName);

            // Validate xor_key parameter
            byte? xorKeyByte = null;
            if (xor_key.HasValue) {
                if (xor_key.Value < 0 || xor_key.Value > 255) {
                    _logger.LogWarning("Exec: Invalid xor_key value: {XorKey}. Must be between 0 and 255", xor_key.Value);
                    return BadRequest(new ExecuteFileResponse {
                        Status = "error",
                        Message = $"Invalid xor_key value: {xor_key.Value}. Must be between 0 and 255"
                    });
                }
                xorKeyByte = (byte)xor_key.Value;
                //_logger.LogInformation("XOR key provided: {XorKey}", xorKeyByte);
            }

            // Validate file upload
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName)) {
                _logger.LogWarning("Exec: Data error: file content size: {Size}, filename: {FileName}",
                    file?.Length ?? 0, file?.FileName ?? "");

                return BadRequest(new ExecuteFileResponse {
                    Status = "error",
                    Message = "Invalid request: filename or file data is missing"
                });
            }

            // Determine target path. Default is OS-specific
            // Typically C:\Users\Public\Downloads\ on Windows, /tmp/ on Linux
            var targetPath = string.IsNullOrWhiteSpace(drop_path) ? DefaultDropPath() : drop_path;
            var filePath = Path.Combine(targetPath, file.FileName);

            // Get file content
            byte[] fileContent;
            using (var memoryStream = new MemoryStream()) {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Start EDR collection
            // This should be before StartProcess() to capture all events
            // But that makes us miss some events like RTP, which seems to be starting on file write?
            // Better do it before writing the file to be sure
            _edrService.StartCollection();

            // Write the file
            _logger.LogInformation("Exec: Writing file: {FilePath}", filePath);
            try {
                executionService.WriteFile(filePath, fileContent, xorKeyByte);
            }
            catch (IOException ioEx) {
                _logger.LogWarning(ioEx, "Exec: File write failed (likely quarantined by AV): {FilePath}", filePath);
                return Ok(new ExecuteFileResponse {
                    Status = "virus",
                    Message = "File write failed - likely quarantined by antivirus"
                });
            }

            // Start the malware
            var (success, pid, errorMessage) = await executionService.StartProcessAsync(executable_args);
            if (!success) {
                if (errorMessage == "virus") {
                    _logger.LogInformation("Exec: Malware execution blocked by antivirus");
                    return Ok(new ExecuteFileResponse {
                        Status = "virus",
                        Message = "Malware execution blocked by antivirus"
                    });
                }

                return StatusCode(500, new ExecuteFileResponse {
                    Status = "error",
                    Message = $"Failed to execute malware: {errorMessage}"
                });
            } else {
                _logger.LogInformation("Exec: Malware executed successfully with PID: {Pid}", pid);
                _executionTracking.SetLastExecutionService(executionService);

                if (pid == 0) {
                    return Ok(new ExecuteFileResponse {
                        Status = "ok",
                        Pid = 0,
                        Message = errorMessage
                    });
                } else {
                    return Ok(new ExecuteFileResponse {
                        Status = "ok",
                        Pid = pid
                    });
                }
            }
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
            _logger.LogInformation("Exec: Kill request received");

            _edrService.StopCollection();

            // Get the last used execution service
            var executionService = _executionTracking.GetLastExecutionService();
            if (executionService == null) {
                _logger.LogWarning("Exec: No execution service found - no execution has been run yet");
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
