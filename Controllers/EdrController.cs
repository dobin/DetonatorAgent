using System.Diagnostics;
using System.Reflection;
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

    [HttpGet("version")]
    public ActionResult<VersionResponse> GetVersion()
    {
        var (version, compilationDate) = GetAutoIncrementedVersion();

        return Ok(new VersionResponse
        {
            Version = version,
            CompilationDate = compilationDate
        });
    }

    private (string Version, DateTime CompilationDate) GetAutoIncrementedVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        
        if (version == null) 
            return ("1.0.0.0", DateTime.MinValue);

        // Reconstruct the build date from the third and fourth components
        int days = version.Build;          // Days since Jan 1, 2000
        int seconds = version.Revision * 2; // Seconds since midnight

        DateTime compilationDate = new DateTime(2000, 1, 1).AddDays(days).AddSeconds(seconds);

        // Convert from UTC to local time
        compilationDate = TimeZoneInfo.ConvertTimeFromUtc(compilationDate, TimeZoneInfo.Local);

        return (version.ToString(), compilationDate);
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
