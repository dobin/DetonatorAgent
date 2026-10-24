using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DetonatorAgent.Models;
using DetonatorAgent.Services;

namespace DetonatorAgent.EdrPlugins;

/// <summary>
/// Resolved configuration for the Rustinel EDR plugin.
/// </summary>
public sealed record RustinelPluginOptions(string BaseDirectory);

/// <summary>
/// Reads Rustinel ECS alert records from daily NDJSON files below the configured
/// Rustinel base directory. Rustinel rotates its alert files at midnight UTC as
/// <c>logs/alerts.json.yyyy-MM-dd</c>.
/// </summary>
[SupportedOSPlatform("windows")]
[EdrPlugin("rustinel", EdrPlatform.Windows)]
public sealed class RustinelEdrPlugin : IEdrService
{
    private const string AlertFilePrefix = "alerts.json.";

    private readonly ILogger<RustinelEdrPlugin> _logger;
    private readonly string _logsDirectory;
    private readonly TimeProvider _timeProvider;
    private DateTime _startTimeUtc;
    private DateTime _stopTimeUtc;
    private string? _observedVersion;

    public RustinelEdrPlugin(ILogger<RustinelEdrPlugin> logger, RustinelPluginOptions options, TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _logsDirectory = Path.Combine(options.BaseDirectory, "logs");
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger.LogInformation("Rustinel Plugin: Reading ECS alerts from {LogsDirectory}", _logsDirectory);
    }

    public bool StartCollection()
    {
        _startTimeUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _stopTimeUtc = default;
        _logger.LogInformation("Rustinel Plugin: Started collection at {StartTime:O}", _startTimeUtc);
        return true;
    }

    public bool StopCollection()
    {
        _stopTimeUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _logger.LogInformation("Rustinel Plugin: Stopped collection at {StopTime:O}", _stopTimeUtc);
        return true;
    }

    public EdrAlertsResponse GetEdrAlerts()
    {
        var response = new EdrAlertsResponse();
        if (_startTimeUtc == default)
        {
            _logger.LogWarning("Rustinel Plugin: StartCollection was not called before GetEdrAlerts");
            return response;
        }

        var endTimeUtc = _stopTimeUtc == default ? _timeProvider.GetUtcNow().UtcDateTime : _stopTimeUtc;
        if (endTimeUtc < _startTimeUtc)
        {
            _logger.LogWarning("Rustinel Plugin: Collection end precedes its start time");
            return response;
        }

        try
        {
            foreach (var date in DatesInRange(_startTimeUtc, endTimeUtc))
            {
                var path = Path.Combine(_logsDirectory, AlertFilePrefix + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                ReadAlertsFromFile(path, _startTimeUtc, endTimeUtc, response.Alerts);
            }

            response.Detected = response.Alerts.Count > 0;
            response.Success = true;
            _logger.LogInformation("Rustinel Plugin: Parsed {Count} alerts between {StartTime:O} and {EndTime:O}",
                response.Alerts.Count, _startTimeUtc, endTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Rustinel Plugin: Failed to read alert files from {LogsDirectory}", _logsDirectory);
        }

        return response;
    }

    public string GetEdrVersion() => _observedVersion is null
        ? $"Rustinel (version unknown; alerts: {_logsDirectory})"
        : $"Rustinel {_observedVersion}";

    private void ReadAlertsFromFile(string path, DateTime startTimeUtc, DateTime endTimeUtc, List<SubmissionAlert> alerts)
    {
        if (!File.Exists(path))
        {
            _logger.LogDebug("Rustinel Plugin: No alert file for this UTC date: {Path}", path);
            return;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (TryParseAlert(line, path, lineNumber, startTimeUtc, endTimeUtc, out var alert))
                alerts.Add(alert);
        }
    }

    private bool TryParseAlert(string rawJson, string sourcePath, int lineNumber, DateTime startTimeUtc, DateTime endTimeUtc, out SubmissionAlert alert)
    {
        alert = null!;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetString(root, "event.kind", out var eventKind) ||
                !string.Equals(eventKind, "alert", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetString(root, "@timestamp", out var timestampText) ||
                !DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
            {
                _logger.LogWarning("Rustinel Plugin: Skipping alert with invalid @timestamp in {Path} line {LineNumber}", sourcePath, lineNumber);
                return false;
            }

            var detectedAtUtc = timestamp.UtcDateTime;
            if (detectedAtUtc < startTimeUtc || detectedAtUtc > endTimeUtc)
                return false;

            if (TryGetString(root, "agent.version", out var version) && !string.IsNullOrWhiteSpace(version))
                _observedVersion = version;

            var category = FirstString(root, "event.category")
                ?? StringValue(root, "event.dataset")
                ?? "Unknown";
            var detectionSource = StringValue(root, "edr.rule.engine")
                ?? StringValue(root, "event.provider")
                ?? "Rustinel";

            alert = new SubmissionAlert
            {
                Source = "Rustinel",
                Raw = rawJson,
                AlertId = CreateAlertId(sourcePath, lineNumber, rawJson),
                Title = StringValue(root, "rule.name") ?? "Unknown",
                Severity = StringValue(root, "edr.rule.severity") ?? "Unknown",
                Category = category,
                DetectionSource = detectionSource,
                DetectedAt = detectedAtUtc,
                AdditionalData = AdditionalData(root)
            };
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Rustinel Plugin: Skipping malformed NDJSON in {Path} line {LineNumber}", sourcePath, lineNumber);
            return false;
        }
    }

    private static IEnumerable<DateTime> DatesInRange(DateTime startTimeUtc, DateTime endTimeUtc)
    {
        for (var date = startTimeUtc.Date; date <= endTimeUtc.Date; date = date.AddDays(1))
            yield return date;
    }

    private static string CreateAlertId(string sourcePath, int lineNumber, string rawJson)
    {
        var identity = $"{Path.GetFileName(sourcePath)}\n{lineNumber}\n{rawJson}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Dictionary<string, object> AdditionalData(JsonElement root)
    {
        var fields = new[] { "rule.id", "event.code", "event.dataset", "event.provider", "event.count", "edr.event.ingest_seq" };
        var data = new Dictionary<string, object>();
        foreach (var field in fields)
        {
            if (!root.TryGetProperty(field, out var value))
                continue;

            var primitive = ToPrimitive(value);
            if (primitive is not null)
                data[field] = primitive;
        }
        return data;
    }

    private static object? ToPrimitive(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static string? FirstString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();
            }
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = StringValue(root, propertyName) ?? string.Empty;
        return value.Length > 0;
    }

    private static string? StringValue(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}