using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

namespace DetonatorAgent.EdrPlugins;

[SupportedOSPlatform("windows")]
public class ExampleEdrPlugin : IEdrService {
    private readonly ILogger<ExampleEdrPlugin> _logger;
    private string _collectedLogs = string.Empty;
    private readonly object _lockObject = new object();

    public ExampleEdrPlugin(ILogger<ExampleEdrPlugin> logger) {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync() {
        try {
            lock (_lockObject) {
                _collectedLogs = string.Empty;
            }

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to start Example EDR log collection");
            return false;
        }
    }

    public async Task<bool> StopCollectionAsync() {
        try {
            await Task.CompletedTask;

            DateTime stopTime;
            lock (_lockObject) {
                stopTime = DateTime.UtcNow;
            }

            var logs = "";

            lock (_lockObject) {
                _collectedLogs = logs;
            }

            _logger.LogInformation("Collected {LogLength} characters of Example EDR events", logs.Length);
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to stop Example EDR log collection");
            return false;
        }
    }

    public async Task<string> GetLogsAsync() {
        await Task.CompletedTask;
        lock (_lockObject) {
            return _collectedLogs;
        }
    }

    public string GetEdrVersion() {
        return "Example EDR 1.0";
    }

    public string GetPluginVersion() {
        return "1.0";
    }
}
