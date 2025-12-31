using DetonatorAgent.Models;
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
        _logger.LogInformation("ExamplePlugin: Started Example EDR log collection");
        return true;
    }

    public bool StopCollection() {
        _logger.LogInformation("ExamplePlugin: Stopped Example EDR log collection");
        return true;
    }

    public EdrAlertsResponse GetEdrAlerts() {
        _logger.LogInformation("ExamplePlugin: Retrieving Example EDR logs");
        return new EdrAlertsResponse { Success = true, Alerts = new List<SubmissionAlert>(), Detected = false };
    }

    public string GetEdrVersion() {
        return "Example EDR 1.0";
    }
}
