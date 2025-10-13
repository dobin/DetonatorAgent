using Microsoft.AspNetCore.Mvc;
using DetonatorAgent.Services;
using DetonatorAgent.Models;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LockController : ControllerBase {
    private readonly ILockService _lockService;
    private readonly ILogger<LockController> _logger;

    public LockController(ILockService lockService, ILogger<LockController> logger) {
        _lockService = lockService;
        _logger = logger;
    }

    [HttpPost("acquire")]
    public ActionResult AcquireLock() {
        try {
            if (!_lockService.TryAcquireLock()) {
                var errorResponse = new LockErrorResponse {
                    Status = "error",
                    Message = "Resource is already in use"
                };

                return Conflict(errorResponse);
            }

            return Ok();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error acquiring lock");
            var errorResponse = new LockErrorResponse {
                Status = "error",
                Message = "Internal server error"
            };
            return StatusCode(500, errorResponse);
        }
    }

    [HttpPost("release")]
    public ActionResult ReleaseLock() {
        try {
            _lockService.ReleaseLock();
            return Ok();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error releasing lock");
            var errorResponse = new LockErrorResponse {
                Status = "error",
                Message = "Internal server error"
            };
            return StatusCode(500, errorResponse);
        }
    }

    [HttpGet("status")]
    public ActionResult<LockStatusResponse> GetLockStatus() {
        try {
            var response = new LockStatusResponse {
                InUse = _lockService.IsInUse
            };

            return Ok(response);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error getting lock status");
            var errorResponse = new LockErrorResponse {
                Status = "error",
                Message = "Internal server error"
            };
            return StatusCode(500, errorResponse);
        }
    }
}
