using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly IEdrService _edrService;
    private readonly IExecutionService _executionService;
    private readonly IAgentLogService _agentLogService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IEdrService edrService, IExecutionService executionService, IAgentLogService agentLogService, ILogger<LogsController> logger)
    {
        _edrService = edrService;
        _executionService = executionService;
        _agentLogService = agentLogService;
        _logger = logger;
    }

    [HttpGet("edr")]
    public async Task<ActionResult<EdrLogsResponse>> GetEdrLogs()
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
            
            return Ok(new EdrLogsResponse
            {
                Logs = logs,
                EdrVersion = edrVersion,
                PluginVersion = pluginVersion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDR logs");
            
            return StatusCode(500, "Failed to retrieve EDR logs");
        }
    }

    [HttpGet("execution")]
    public async Task<ActionResult<ExecutionLogsResponse>> GetExecutionLogs()
    {
        try
        {
            _logger.LogInformation("Retrieving execution logs");
            
            var (pid, stdout, stderr) = await _executionService.GetExecutionLogsAsync();
            
            var response = new ExecutionLogsResponse
            {
                Pid = pid,
                Stdout = stdout,
                Stderr = stderr
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution logs");
            
            return StatusCode(500, "Failed to retrieve execution logs");
        }
    }

    [HttpGet("agent")]
    public ActionResult<List<string>> GetAgentLogs()
    {
        try
        {
            _logger.LogInformation("Retrieving agent logs");
            
            var logs = _agentLogService.GetAgentLogs();
            
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent logs");
            
            return StatusCode(500, "Failed to retrieve agent logs");
        }
    }

    [HttpDelete("agent")]
    public ActionResult<string> ClearAgentLogs()
    {
        try
        {
            _logger.LogInformation("Clearing agent logs");
            
            _agentLogService.ClearLogs();
            
            return Ok("Agent logs cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing agent logs");
            
            return StatusCode(500, "Failed to clear agent logs");
        }
    }
}
