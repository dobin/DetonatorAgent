using System.Text.Json;
using DetonatorAgent.EdrPlugins;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DetonatorAgent.Tests;

public sealed class RustinelEdrPluginTests : IDisposable
{
    private readonly string _baseDirectory = Path.Combine(Path.GetTempPath(), "DetonatorAgent.Tests", Guid.NewGuid().ToString("N"));

    private const string RealRustinelAlert = """
        {"@timestamp":"2026-10-24T08:36:33.627334700Z","edr.event.ingest_seq":1204,"edr.event.provenance":[{"field":"ParentImage","fidelity":"derived"},{"field":"ParentCommandLine","fidelity":"derived"},{"field":"OriginalFileName","fidelity":"derived"},{"field":"Product","fidelity":"derived"},{"field":"Description","fidelity":"derived"},{"field":"Company","fidelity":"derived"},{"field":"FileVersion","fidelity":"derived"}],"edr.process.windows_metadata":{"command_line_source":"classic","conflicting_live_command_line":null,"classic_command_line":null,"command_line_may_be_truncated":false,"user_sid":"S-1-5-21-704188021-1311137335-3068894510-1001","session_id":2},"ecs.version":"9.5.0","event.kind":"alert","event.category":["process"],"event.type":["start"],"event.action":"process-start","event.code":"1","event.severity":25,"event.module":"edr","event.dataset":"edr.process","event.provider":"etw","host.os.type":"windows","host.os.family":"windows","rule.name":"Example - Whoami Execution (Windows)","rule.description":"Detects execution of whoami.exe (demo rule).","rule.id":"sigma::d4f5e6b2-3c7a-4e1b-9f2a-123456789abc","rule.author":["Rustinel"],"edr.rule.severity":"Low","edr.rule.engine":"Sigma","edr.sigma.level":"low","edr.sigma.status":"experimental","process.executable":"C:\\Windows\\System32\\whoami.exe","process.name":"whoami.exe","process.command_line":"whoami","process.pid":9048,"process.parent.executable":"C:\\Windows\\System32\\cmd.exe","process.parent.name":"cmd.exe","process.parent.command_line":"\"C:\\WINDOWS\\system32\\cmd.exe\" ","process.parent.pid":2560,"edr.process.integrity_level":"Medium","process.pe.original_file_name":"whoami.exe","process.pe.product":"Microsoft® Windows® Operating System","process.pe.description":"whoami - displays logged on user information","process.pe.company":"Microsoft Corporation","process.pe.file_version":"10.0.26100.1882 (WinBuild.160101.0800)","user.id":"S-1-5-21-704188021-1311137335-3068894510-1001","related.user":["S-1-5-21-704188021-1311137335-3068894510-1001"],"host.id":"3819af7978aa93ffe21abcf846eff3e8","host.name":"desktop-k8rgod4","agent.id":"0aebaee6-3af0-4486-8a1f-2ec26ccab29f","agent.type":"rustinel","agent.version":"1.10.0"}
        """;

    [Fact]
    public void AlertSourceValidation_RequiresLogsDirectoryAndAlertFile()
    {
        Assert.False(RustinelAlertSource.TryValidate(_baseDirectory, out var missingDirectoryError));
        Assert.Contains("log directory does not exist", missingDirectoryError);

        Directory.CreateDirectory(Path.Combine(_baseDirectory, "logs"));
        Assert.False(RustinelAlertSource.TryValidate(_baseDirectory, out var missingFileError));
        Assert.Contains("No Rustinel alert files", missingFileError);

        File.WriteAllText(Path.Combine(_baseDirectory, "logs", "alerts.json.2026-10-24"), RealRustinelAlert);
        Assert.True(RustinelAlertSource.TryValidate(_baseDirectory, out var successError));
        Assert.Equal(string.Empty, successError);
    }

    [Fact]
    public void GetEdrAlerts_ParsesRealRustinelAlert()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "logs"));
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 10, 24, 8, 36, 0, TimeSpan.Zero));
        var plugin = CreatePlugin(clock);
        Assert.True(plugin.StartCollection());
        File.AppendAllText(CurrentAlertFile(clock.GetUtcNow()), RealRustinelAlert + Environment.NewLine);
        clock.UtcNow = new DateTimeOffset(2026, 10, 24, 8, 37, 0, TimeSpan.Zero);
        Assert.True(plugin.StopCollection());

        var response = plugin.GetEdrAlerts();

        var alert = Assert.Single(response.Alerts);
        Assert.True(response.Success);
        Assert.Equal("Example - Whoami Execution (Windows)", alert.Title);
        Assert.Equal("Low", alert.Severity);
        Assert.Equal("process", alert.Category);
        Assert.Equal("Sigma", alert.DetectionSource);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T08:36:33.627334700Z").UtcDateTime, alert.DetectedAt!.Value);
        Assert.Equal("sigma::d4f5e6b2-3c7a-4e1b-9f2a-123456789abc", alert.AdditionalData!["rule.id"]);
        Assert.Equal(1204L, alert.AdditionalData["edr.event.ingest_seq"]);
        Assert.Equal("Rustinel 1.10.0", plugin.GetEdrVersion());
    }

    [Fact]
    public void GetEdrAlerts_ParsesCurrentUtcAlertAndReturnsStableId()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "logs"));
        var plugin = CreatePlugin();
        Assert.True(plugin.StartCollection());

        AppendAlert(CreateAlert(DateTimeOffset.UtcNow, "Example rule", "High", new[] { "process" }));

        var first = plugin.GetEdrAlerts();
        var second = plugin.GetEdrAlerts();

        var alert = Assert.Single(first.Alerts);
        Assert.True(first.Success);
        Assert.True(first.Detected);
        Assert.Equal("Rustinel", alert.Source);
        Assert.Equal("Example rule", alert.Title);
        Assert.Equal("High", alert.Severity);
        Assert.Equal("process", alert.Category);
        Assert.Equal("Sigma", alert.DetectionSource);
        Assert.Equal(alert.AlertId, Assert.Single(second.Alerts).AlertId);
        Assert.Equal("1.10.0", plugin.GetEdrVersion().Replace("Rustinel ", string.Empty));
        Assert.Equal("sigma::example", alert.AdditionalData!["rule.id"]);
        Assert.Equal(42L, alert.AdditionalData["edr.event.ingest_seq"]);
    }

    [Fact]
    public void GetEdrAlerts_SkipsMalformedAndNonAlertRecords()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "logs"));
        var plugin = CreatePlugin();
        Assert.True(plugin.StartCollection());

        var path = CurrentAlertFile();
        File.AppendAllText(path, "not JSON\n");
        File.AppendAllText(path, "{\"event.kind\":\"event\",\"@timestamp\":\"2026-01-01T00:00:00Z\"}\n");
        File.AppendAllText(path, JsonSerializer.Serialize(CreateAlert(DateTimeOffset.UtcNow, "Valid", "Low", Array.Empty<string>())) + "\n");

        var response = plugin.GetEdrAlerts();

        var alert = Assert.Single(response.Alerts);
        Assert.True(response.Success);
        Assert.Equal("edr.process", alert.Category);
    }

    [Fact]
    public void GetEdrAlerts_ExcludesRecordsOutsideCollectionWindow()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "logs"));
        var plugin = CreatePlugin();
        Assert.True(plugin.StartCollection());

        AppendAlert(CreateAlert(DateTimeOffset.UtcNow.AddMinutes(-5), "Old", "High", new[] { "process" }));

        var response = plugin.GetEdrAlerts();

        Assert.True(response.Success);
        Assert.Empty(response.Alerts);
        Assert.False(response.Detected);
    }

    private RustinelEdrPlugin CreatePlugin(TimeProvider? timeProvider = null) =>
        new(NullLogger<RustinelEdrPlugin>.Instance, new RustinelPluginOptions(_baseDirectory), timeProvider);

    private void AppendAlert(object alert) => File.AppendAllText(CurrentAlertFile(), JsonSerializer.Serialize(alert) + Environment.NewLine);

    private string CurrentAlertFile() => Path.Combine(_baseDirectory, "logs", $"alerts.json.{DateTime.UtcNow:yyyy-MM-dd}");

    private string CurrentAlertFile(DateTimeOffset timestamp) =>
        Path.Combine(_baseDirectory, "logs", $"alerts.json.{timestamp.UtcDateTime:yyyy-MM-dd}");

    private static object CreateAlert(DateTimeOffset timestamp, string name, string severity, string[] categories) => new Dictionary<string, object?>
    {
        ["@timestamp"] = timestamp.ToString("O"),
        ["event.kind"] = "alert",
        ["event.category"] = categories,
        ["event.dataset"] = "edr.process",
        ["event.provider"] = "etw",
        ["edr.event.ingest_seq"] = 42,
        ["rule.name"] = name,
        ["rule.id"] = "sigma::example",
        ["edr.rule.severity"] = severity,
        ["edr.rule.engine"] = "Sigma",
        ["agent.version"] = "1.10.0"
    };

    public void Dispose()
    {
        if (Directory.Exists(_baseDirectory))
            Directory.Delete(_baseDirectory, recursive: true);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
