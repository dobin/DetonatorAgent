using DetonatorAgent.Models;
using DetonatorAgent.Services;

namespace DetonatorAgent.EdrPlugins;

/// <summary>
/// Cross-platform sample EDR plugin that reads alerts from a plain text log file.
///
/// Behavior:
///   - On StartCollection(): remembers the current end-of-file offset of the
///     configured log file (or 0 if the file does not exist yet).
///   - On StopCollection(): remembers the current end-of-file offset as the
///     upper bound for GetEdrAlerts().
///   - On GetEdrAlerts(): reads the byte range [start, stop) (or [start, EOF)
///     if StopCollection was not yet called) and emits one alert per
///     non-empty line.
///
/// Line format expected (all fields optional, tab or `|` separated):
///     &lt;title&gt; | &lt;severity&gt; | &lt;category&gt;
/// If the line does not match, the whole line becomes the title.
///
/// Configuration:
///   Environment variable DETONATOR_EDR_LOGFILE overrides the default path.
///   Default path: /var/log/detonator-edr.log (Linux) or
///                 C:\ProgramData\DetonatorAgent\edr.log (Windows).
/// </summary>
[EdrPlugin("logfile", EdrPlatform.Cross, LinuxDefault = true)]
public class LogFileEdrPlugin : IEdrService {
    private readonly ILogger<LogFileEdrPlugin> _logger;
    private readonly string _logFilePath;

    private long _startOffset = -1;
    private long _stopOffset = -1;

    public LogFileEdrPlugin(ILogger<LogFileEdrPlugin> logger) {
        _logger = logger;
        _logFilePath = Environment.GetEnvironmentVariable("DETONATOR_EDR_LOGFILE")
            ?? DefaultLogPath();
        _logger.LogInformation("LogFile Plugin: Using log file {Path}", _logFilePath);
    }

    private static string DefaultLogPath() {
        if (OperatingSystem.IsWindows()) {
            return @"C:\ProgramData\DetonatorAgent\edr.log";
        }
        return "/var/log/detonator-edr.log";
    }

    private long CurrentFileLength() {
        try {
            var info = new FileInfo(_logFilePath);
            return info.Exists ? info.Length : 0;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "LogFile Plugin: Failed to stat {Path}", _logFilePath);
            return 0;
        }
    }

    public bool StartCollection() {
        _startOffset = CurrentFileLength();
        _stopOffset = -1;
        _logger.LogInformation("LogFile Plugin: Started collection at offset {Offset}", _startOffset);
        return true;
    }

    public bool StopCollection() {
        _stopOffset = CurrentFileLength();
        _logger.LogInformation("LogFile Plugin: Stopped collection at offset {Offset}", _stopOffset);
        return true;
    }

    public EdrAlertsResponse GetEdrAlerts() {
        var response = new EdrAlertsResponse {
            Success = false,
            Alerts = new List<SubmissionAlert>(),
            Detected = false
        };

        if (_startOffset < 0) {
            _logger.LogWarning("LogFile Plugin: StartCollection was not called before GetEdrAlerts");
            return response;
        }

        if (!File.Exists(_logFilePath)) {
            _logger.LogInformation("LogFile Plugin: Log file {Path} does not exist yet", _logFilePath);
            response.Success = true;
            return response;
        }

        try {
            long upper = _stopOffset >= 0 ? _stopOffset : CurrentFileLength();
            if (upper < _startOffset) {
                // File was truncated / rotated. Read from 0 to upper.
                _logger.LogWarning("LogFile Plugin: File appears rotated (upper < start), reading from 0");
                _startOffset = 0;
            }

            long length = upper - _startOffset;
            if (length <= 0) {
                response.Success = true;
                return response;
            }

            using var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_startOffset, SeekOrigin.Begin);
            var buffer = new byte[length];
            int read = fs.Read(buffer, 0, (int)Math.Min(length, int.MaxValue));
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

            foreach (var rawLine in text.Split('\n')) {
                var line = rawLine.TrimEnd('\r').Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var alert = ParseLine(line);
                response.Alerts.Add(alert);
            }

            response.Detected = response.Alerts.Count > 0;
            response.Success = true;
            _logger.LogInformation("LogFile Plugin: Parsed {Count} alerts from {Path}",
                response.Alerts.Count, _logFilePath);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "LogFile Plugin: Failed to read {Path}", _logFilePath);
        }

        return response;
    }

    private static SubmissionAlert ParseLine(string line) {
        // Accept `|` or tab as field separator. First 3 fields: title, severity, category.
        var parts = line.Contains('|')
            ? line.Split('|')
            : line.Split('\t');
        string title = parts.Length > 0 ? parts[0].Trim() : line;
        string severity = parts.Length > 1 ? parts[1].Trim() : "Unknown";
        string category = parts.Length > 2 ? parts[2].Trim() : "Unknown";

        return new SubmissionAlert {
            Source = "LogFile",
            Raw = line,
            AlertId = Guid.NewGuid().ToString(),
            Title = title,
            Severity = severity,
            Category = category,
            DetectionSource = "LogFile",
            DetectedAt = DateTime.Now,
            AdditionalData = new Dictionary<string, object>()
        };
    }

    public string GetEdrVersion() {
        return $"LogFile Plugin (path: {_logFilePath})";
    }
}
