using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogService logService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<string>>> GetLogs()
    {
        try
        {
            _logger.LogInformation("Retrieving logs from {Platform}", Environment.OSVersion.Platform);
            
            var logs = await _logService.GetLogsAsync();
            
            return Ok(new ApiResponse<string>
            {
                Success = true,
                Data = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Error = "Failed to retrieve logs"
            });
        }
    }
}
