using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecuteController : ControllerBase
{
    private readonly IExecutionService _executionService;
    private readonly ILogger<ExecuteController> _logger;

    public ExecuteController(IExecutionService executionService, ILogger<ExecuteController> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    [HttpPost("exec")]
    public async Task<ActionResult<ExecuteFileResponse>> ExecuteFile([FromForm] IFormFile file, 
        [FromForm] string? path = null, [FromForm] string? fileargs = null)
    {
        try
        {
            // Validate file upload
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName))
            {
                _logger.LogWarning("Data error: file content size: {Size}, filename: {FileName}", 
                    file?.Length ?? 0, file?.FileName ?? "");
                
                return BadRequest(new ExecuteFileResponse
                {
                    Status = "error",
                    Message = "Invalid request: filename or file data is missing"
                });
            }

            // Determine path
            var targetPath = string.IsNullOrWhiteSpace(path) ? @"C:\RedEdr\data\" : path;
            if (!targetPath.EndsWith(@"\"))
            {
                targetPath += @"\";
            }
            var filePath = Path.Combine(targetPath, file.FileName);

            // Read file content
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Write the malware
            _logger.LogInformation("Writing malware: {FilePath}", filePath);
            if (!await _executionService.WriteMalwareAsync(filePath, fileContent))
            {
                _logger.LogError("Failed to write malware to {FilePath}", filePath);
                return StatusCode(500, new ExecuteFileResponse
                {
                    Status = "error",
                    Message = "Failed to write malware file"
                });
            }

            // Start the malware
            _logger.LogInformation("Executing malware: {FilePath}", filePath);
            var (success, pid, errorMessage) = await _executionService.StartProcessAsync(filePath, fileargs);
            
            if (!success)
            {
                if (errorMessage == "virus")
                {
                    _logger.LogInformation("Malware execution blocked by antivirus");
                    return Ok(new ExecuteFileResponse
                    {
                        Status = "virus",
                        Pid = pid
                    });
                }

                return StatusCode(500, new ExecuteFileResponse
                {
                    Status = "error",
                    Message = $"Failed to execute malware: {errorMessage}"
                });
            }

            return Ok(new ExecuteFileResponse
            {
                Status = "ok",
                Pid = pid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /api/exec");
            return StatusCode(500, new ExecuteFileResponse
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("kill")]
    public async Task<ActionResult<KillResponse>> KillLastExecution()
    {
        try
        {
            _logger.LogInformation("Kill request received");
            
            var (success, errorMessage) = await _executionService.KillLastExecutionAsync();
            
            if (!success)
            {
                return StatusCode(500, new KillResponse
                {
                    Status = "error",
                    Message = errorMessage ?? "Failed to kill last execution"
                });
            }

            return Ok(new KillResponse
            {
                Status = "ok",
                Message = errorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /api/kill");
            return StatusCode(500, new KillResponse
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }
}
