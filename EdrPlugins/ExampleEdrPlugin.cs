using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

namespace DetonatorAgent.EdrPlugins;

[SupportedOSPlatform("windows")]
public class ExampleEdrPlugin : IEdrService {
    private readonly ILogger<ExampleEdrPlugin> _logger;

    public ExampleEdrPlugin(ILogger<ExampleEdrPlugin> logger) {
        _logger = logger;
    }

    public bool StartCollection() {
        _logger.LogInformation("Started Example EDR log collection");
        return true;
    }

    public bool StopCollection() {
        _logger.LogInformation("Stopped Example EDR log collection");
        return true;
    }

    public string GetLogs() {
        _logger.LogInformation("Retrieving Example EDR logs");
        return "<ExampleLogs><Log>Example log entry 1</Log><Log>Example log entry 2</Log></ExampleLogs>";
    }

    public string GetEdrVersion() {
        return "Example EDR 1.0";
    }

    public string GetPluginVersion() {
        return "1.0";
    }
}
