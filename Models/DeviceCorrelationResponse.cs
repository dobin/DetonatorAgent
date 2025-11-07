namespace DetonatorAgent.Models;

public class DeviceCorrelationResponse
{
    public string Hostname { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string OSVersion { get; set; } = string.Empty;
}
