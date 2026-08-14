using DetonatorAgent.Models;
using DetonatorAgent.Services;

namespace DetonatorAgent.EdrPlugins;

/// <summary>
/// Null EDR plugin: does nothing and reports no alerts. Selected via
/// `--edr none`. Use when you only want the detonation functionality of
/// DetonatorAgent without any EDR log collection.
/// </summary>
[EdrPlugin("none", EdrPlatform.Cross)]
public class NoneEdrPlugin : IEdrService {
    private readonly ILogger<NoneEdrPlugin> _logger;

    public NoneEdrPlugin(ILogger<NoneEdrPlugin> logger) {
        _logger = logger;
    }

    public bool StartCollection() {
        _logger.LogInformation("NonePlugin: EDR collection disabled (--edr none)");
        return true;
    }

    public bool StopCollection() {
        return true;
    }

    public EdrAlertsResponse GetEdrAlerts() {
        return new EdrAlertsResponse {
            Success = true,
            Alerts = new List<SubmissionAlert>(),
            Detected = false
        };
    }

    public string GetEdrVersion() {
        return "none";
    }
}
