using DetonatorAgent.Services;
using System.Diagnostics;

namespace DetonatorAgent.EdrPlugins;

public class ElasticEdrPlugin : IEdrService
{
    private readonly ILogger<ElasticEdrPlugin> _logger;

    public ElasticEdrPlugin(ILogger<ElasticEdrPlugin> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync()
    {
        _logger.LogInformation("Elastic EDR collection not yet implemented");
        await Task.CompletedTask;
        return true; // Return true to not break the workflow
    }

    public async Task<bool> StopCollectionAsync()
    {
        _logger.LogInformation("Elastic EDR collection not yet implemented");
        await Task.CompletedTask;
        return true;
    }

    public async Task<string> GetLogsAsync()
    {
        await Task.CompletedTask;
        return "<Events>\n<!-- Elastic EDR collection not yet implemented -->\n</Events>";
    }

    public string GetEdrVersion()
    {
        return "Elastic EDR Not Available";
    }

    public string GetPluginVersion()
    {
        return "1.0";
    }
}
