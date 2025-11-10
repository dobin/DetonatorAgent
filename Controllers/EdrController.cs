using System.Diagnostics;
using System.Runtime.InteropServices;
using DetonatorAgent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;

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

    [HttpGet("sysinfo")]
    public ActionResult<DeviceCorrelationResponse> GetSystemInfo()
    {
        var response = new DeviceCorrelationResponse
        {
            Hostname = GetHostName(),
            OSVersion = RuntimeInformation.OSDescription
        };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                response.DeviceId = FetchSenseId();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Defender device identifiers");
            }
        }

        return Ok(response);
    }

    private static string GetHostName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Environment.MachineName;
        }

        var hostname = ReadRegistryString(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Hostname");
        if (!string.IsNullOrWhiteSpace(hostname))
        {
            return hostname;
        }

        return Environment.MachineName;
    }

    private static string? FetchSenseId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var deviceId = ReadRegistryString(@"SOFTWARE\Microsoft\Windows Advanced Threat Protection", "SenseId");
        return string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
    }

    private static string? ReadRegistryString(string path, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

}
