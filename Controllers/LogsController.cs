using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly IEdrService _edrService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogService logService, IEdrService edrService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _edrService = edrService;
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

    [HttpGet("edr")]
    public async Task<ActionResult<ApiResponse<EdrLogsResponse>>> GetEdrLogs()
    {
        try
        {
            _logger.LogInformation("Retrieving EDR logs - stopping collection and getting events");
            
            // Stop EDR collection first (as per the requirement)
            var stopResult = await _edrService.StopCollectionAsync();
            if (!stopResult)
            {
                _logger.LogWarning("Failed to stop EDR collection, but continuing to get logs");
            }
            
            // Get the collected logs
            var logs = await _edrService.GetLogsAsync();
            var edrVersion = _edrService.GetEdrVersion();
            var pluginVersion = _edrService.GetPluginVersion();
            
            var response = new EdrLogsResponse
            {
                Logs = logs,
                EdrVersion = edrVersion,
                PluginVersion = pluginVersion
            };
            
            return Ok(new ApiResponse<EdrLogsResponse>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDR logs");
            
            return StatusCode(500, new ApiResponse<EdrLogsResponse>
            {
                Success = false,
                Error = "Failed to retrieve EDR logs"
            });
        }
    }
}
