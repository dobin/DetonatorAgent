using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

namespace DetonatorAgent.EdrPlugins;

[SupportedOSPlatform("windows")]
public class ExampleEdrService : IEdrService
{
    private readonly ILogger<ExampleEdrService> _logger;
    private string _collectedLogs = string.Empty;
    private readonly object _lockObject = new object();

    public ExampleEdrService(ILogger<ExampleEdrService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync()
    {
        try
        {
            lock (_lockObject)
            {
                _collectedLogs = string.Empty;
            }
            
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Windows Defender EDR log collection");
            return false;
        }
    }

    public async Task<bool> StopCollectionAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            DateTime stopTime;
            lock (_lockObject)
            {
                stopTime = DateTime.UtcNow;
            }

            var logs = "";
            
            lock (_lockObject)
            {
                _collectedLogs = logs;
            }

            _logger.LogInformation("Collected {LogLength} characters of Windows Defender events", logs.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Windows Defender EDR log collection");
            return false;
        }
    }

    public async Task<string> GetLogsAsync()
    {
        await Task.CompletedTask;
        lock (_lockObject)
        {
            return _collectedLogs;
        }
    }

    public string GetEdrVersion()
    {
        return "Windows Defender 1.0";
    }

    public string GetPluginVersion()
    {
        return "1.0";
    }
}
