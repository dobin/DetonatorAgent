using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase {
    private readonly IExecutionServiceProvider _executionServiceProvider;
    private readonly ILogger<ExecuteController> _logger;

    public ExecuteController(IExecutionServiceProvider executionServiceProvider, ILogger<ExecuteController> logger) {
        _executionServiceProvider = executionServiceProvider;
        _logger = logger;
    }

    [HttpGet("types")]
    public ActionResult<ExecutionTypesResponse> GetExecutionTypes() {
        try {
            var availableTypes = _executionServiceProvider.GetAvailableExecutionTypes().ToList();
            var defaultType = _executionServiceProvider.GetDefaultExecutionTypeName();
            
            _logger.LogInformation("Returning {Count} available execution types, default: {Default}", 
                availableTypes.Count, defaultType);

            return Ok(new ExecutionTypesResponse {
                Types = availableTypes,
                Default = defaultType
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in /api/execute/types");
            return StatusCode(500, new ExecutionTypesResponse {
                Types = new List<string>(),
                Default = string.Empty
            });
        }
    }

    [HttpPost("exec")]
    public async Task<ActionResult<ExecuteFileResponse>> ExecuteFile([FromForm] IFormFile file,
        [FromForm] string? drop_path = null, [FromForm] string? executable_args = null, [FromForm] string? executable_name = null,
        [FromForm] string? execution_mode = null, [FromForm] int? xor_key = null) {
        try {
            // Get the execution service based on execution_mode parameter
            var executionService = _executionServiceProvider.GetExecutionService(execution_mode);
            if (executionService == null) {
                var availableTypes = string.Join(", ", _executionServiceProvider.GetAvailableExecutionTypes());
                _logger.LogWarning("Invalid execution type: {ExecutionType}. Available: {AvailableTypes}", 
                    execution_mode, availableTypes);
                
                return BadRequest(new ExecuteFileResponse {
                    Status = "error",
                    Message = $"Invalid execution type '{execution_mode}'. Available types: {availableTypes}"
                });
            }

            _logger.LogInformation("Using execution type: {ExecutionType}", 
                execution_mode ?? "default");

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

            // Read file content
            byte[] fileContent;
            using (var memoryStream = new MemoryStream()) {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Write the malware (always write first, whether ZIP or executable)
            _logger.LogInformation("Writing file: {FilePath}", filePath);
            if (!await executionService.WriteMalwareAsync(filePath, fileContent, xorKeyByte)) {
                _logger.LogError("Failed to write file to {FilePath}", filePath);
                return StatusCode(500, new ExecuteFileResponse {
                    Status = "error",
                    Message = "Failed to write file"
                });
            }

            // Prepare file for execution (handles ZIP, RAR, or ISO extraction/mounting)
            var (prepareSuccess, actualFilePath, prepareError) = await executionService.PrepareFileForExecutionAsync(filePath, executable_name);
            if (!prepareSuccess) {
                return BadRequest(new ExecuteFileResponse {
                    Status = "error",
                    Message = prepareError ?? "Failed to prepare file for execution"
                });
            }

            // Start the malware (use actualFilePath which might be extracted file or original file)
            _logger.LogInformation("Executing file: {FilePath}", actualFilePath);
            var (success, pid, errorMessage) = await executionService.StartProcessAsync(actualFilePath!, executable_args);

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
            var executionService = _executionServiceProvider.GetLastUsedExecutionService();
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
