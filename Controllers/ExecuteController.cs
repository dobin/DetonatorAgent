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

    [HttpPost]
    public async Task<ActionResult<ApiResponse<string>>> ExecuteCommand([FromBody] ExecuteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Error = "Command cannot be empty"
            });
        }

        try
        {
            _logger.LogInformation("Executing command on {Platform}: {Command}", 
                Environment.OSVersion.Platform, request.Command);
            
            var result = await _executionService.ExecuteCommandAsync(request.Command);
            
            return Ok(new ApiResponse<string>
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", request.Command);
            
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Error = "Failed to execute command"
            });
        }
    }
}
