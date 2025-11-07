using System.Diagnostics;
using System.Runtime.InteropServices;
using DetonatorAgent.Models;
using Microsoft.AspNetCore.Mvc;

namespace DetonatorAgent.Controllers;

[ApiController]
[Route("api/edr")]
public class EdrController : ControllerBase
{
    private readonly ILogger<EdrController> _logger;

    public EdrController(ILogger<EdrController> logger)
    {
        _logger = logger;
    }

    [HttpGet("correlation")]
    public ActionResult<DeviceCorrelationResponse> GetCorrelationInfo()
    {
        var response = new DeviceCorrelationResponse
        {
            Hostname = Environment.MachineName,
            OSVersion = RuntimeInformation.OSDescription
        };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                response.DeviceId = FetchDefenderDeviceId();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Defender device ID via Get-MpComputerStatus");
            }
        }

        return Ok(response);
    }

    private static string? FetchDefenderDeviceId()
    {
        var psCommand = "Try { $id = Get-MpComputerStatus | Select-Object -ExpandProperty ComputerID; if ($id) { $id.Trim() } } Catch { '' }";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoLogo -NoProfile -Command \"{psCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        var deviceId = output?.Trim();
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
    }
}
