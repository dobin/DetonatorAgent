using DetonatorAgent.Services;
using System.Diagnostics;

namespace DetonatorAgent.EdrPlugins;

public class LinuxEdrService : IEdrService
{
    private readonly ILogger<LinuxEdrService> _logger;

    public LinuxEdrService(ILogger<LinuxEdrService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync()
    {
        _logger.LogInformation("EDR collection not implemented for Linux platform");
        await Task.CompletedTask;
        return true; // Return true to not break the workflow
    }

    public async Task<bool> StopCollectionAsync()
    {
        _logger.LogInformation("EDR collection not implemented for Linux platform");
        await Task.CompletedTask;
        return true;
    }

    public async Task<string> GetLogsAsync()
    {
        await Task.CompletedTask;
        return "<Events>\n<!-- EDR collection not implemented for Linux platform -->\n</Events>";
    }

    public string GetEdrVersion()
    {
        return "Linux EDR Not Available";
    }

    public string GetPluginVersion()
    {
        return "1.0";
    }
}
