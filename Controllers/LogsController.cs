using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase {
    private readonly IEdrService _edrService;
    private readonly ExecutionTrackingService _executionTracking;
    private readonly IAgentLogService _agentLogService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IEdrService edrService, ExecutionTrackingService executionTracking, IAgentLogService agentLogService, ILogger<LogsController> logger) {
        _edrService = edrService;
        _executionTracking = executionTracking;
        _agentLogService = agentLogService;
        _logger = logger;
    }

    [HttpGet("edr")]
    public async Task<ActionResult<string>> GetEdrLogs() {
        try {
            var logs = _edrService.GetLogs();
            return Ok(logs);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogsController: Error retrieving EDR logs");
            return StatusCode(500, "Failed to retrieve EDR logs");
        }
    }

    [HttpGet("edrversion")]
    public async Task<ActionResult<EdrVersionResponse>> GetEdrVersion() {
        try {
            var edrVersion = _edrService.GetEdrVersion();
            return Ok(new EdrVersionResponse {
                EdrVersion = edrVersion,
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogsController: Error retrieving EDR version");

            return StatusCode(500, "Failed to retrieve EDR version");
        }
    }

    [HttpGet("execution")]
    public async Task<ActionResult<ExecutionLogsResponse>> GetExecutionLogs() {
        try {
            // Get the last used execution service
            var executionService = _executionTracking.GetLastExecutionService();
            if (executionService == null) {
                _logger.LogWarning("LogsController: No execution service found - no execution has been run yet");
                return BadRequest(new ExecutionLogsResponse {
                    Pid = 0,
                    Stdout = "",
                    Stderr = "No execution has been run yet"
                });
            }

            var (pid, stdout, stderr) = await executionService.GetExecutionLogsAsync();

            var response = new ExecutionLogsResponse {
                Pid = pid,
                Stdout = stdout,
                Stderr = stderr
            };

            return Ok(response);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogsController: Error retrieving execution logs");
            return StatusCode(500, "Failed to retrieve execution logs");
        }
    }

    [HttpGet("agent")]
    public ActionResult<string> GetAgentLogs() {
        try {
            _logger.LogInformation("LogsController: Retrieving agent logs");

            var logs = _agentLogService.GetAgentLogs();
            var logsString = string.Join("\n", logs);

            return Ok(logsString);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogsController: Error retrieving agent logs");
            return StatusCode(500, "Failed to retrieve agent logs");
        }
    }

    [HttpDelete("agent")]
    public ActionResult<string> ClearAgentLogs() {
        try {
            _logger.LogInformation("LogsController: Clearing agent logs");
            _agentLogService.ClearLogs();
            return Ok("Agent logs cleared successfully");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogsController: Error clearing agent logs");
            return StatusCode(500, "Failed to clear agent logs");
        }
    }
}
