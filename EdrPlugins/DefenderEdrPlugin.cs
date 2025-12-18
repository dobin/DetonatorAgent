using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

namespace DetonatorAgent.EdrPlugins;

[SupportedOSPlatform("windows")]
public class DefenderEdrPlugin : IEdrService {
    private readonly ILogger<DefenderEdrPlugin> _logger;

    private DateTime _startTime;
    private DateTime _stopTime;

    public DefenderEdrPlugin(ILogger<DefenderEdrPlugin> logger) {
        _logger = logger;
    }


    public bool StartCollection() {
        _startTime = DateTime.UtcNow;
        _logger.LogInformation("Started Windows Defender EDR log collection at {StartTime}", 
            _startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        return true;
    }


    public bool StopCollection() {
        _stopTime = DateTime.UtcNow;
        _logger.LogInformation("Stopping Windows Defender EDR log collection at {StopTime}",
            _stopTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        return true;
    }


    public string GetLogs() {
        var logs = GetDefenderEventsSince();
        return logs;
    }


    [SupportedOSPlatform("windows")]
    private string GetDefenderEventsSince() {
        try {
            // Convert to the format expected by Event Log queries (ISO 8601 format)
            // Windows Event Log expects UTC time in this specific format
            string startTimeStr = _startTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
            string endTimeStr = _stopTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
            if (_stopTime == default) {
                // If no end time (after killing the process) is given, we just assume its until NOW
                endTimeStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
            }

            // XPath query for filtering events by time range
            string query = $"*[System[TimeCreated[@SystemTime >= '{startTimeStr}' and @SystemTime <= '{endTimeStr}']]]";

            _logger.LogInformation("Querying Windows Defender events from {StartTime} to {EndTime}",
                startTimeStr, endTimeStr);
            _logger.LogDebug("With query string: {QueryString}", query);

            var allEvents = new StringBuilder();
            allEvents.AppendLine("<Events>");

            try {
                var eventQuery = new EventLogQuery("Microsoft-Windows-Windows Defender/Operational", PathType.LogName, query);
                using var eventReader = new EventLogReader(eventQuery);

                EventRecord? eventRecord;
                int eventCount = 0;

                while ((eventRecord = eventReader.ReadEvent()) != null) {
                    using (eventRecord) {
                        try {
                            // Convert the event to XML format
                            string eventXml = eventRecord.ToXml();
                            allEvents.AppendLine(eventXml);
                            eventCount++;
                        }
                        catch (Exception ex) {
                            _logger.LogWarning(ex, "Failed to convert event {EventId} to XML", eventRecord.Id);
                        }
                    }
                }

                _logger.LogInformation("Retrieved {EventCount} Windows Defender events", eventCount);
            }
            catch (EventLogNotFoundException) {
                _logger.LogWarning("Windows Defender Operational log not found. This might be expected on some systems.");
                allEvents.AppendLine("<!-- Windows Defender Operational log not found -->");
            }
            catch (UnauthorizedAccessException) {
                _logger.LogWarning("Access denied to Windows Defender Operational log. Administrator privileges may be required.");
                allEvents.AppendLine("<!-- Access denied to Windows Defender Operational log -->");
            }

            allEvents.AppendLine("</Events>");
            return allEvents.ToString();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error retrieving Windows Defender events");
            return "<Events>\n<!-- Error retrieving events: " + ex.Message + " -->\n</Events>";
        }
    }

    
    public string GetEdrVersion() {
        return "Windows Defender 1.0";
    }


    public string GetPluginVersion() {
        return "1.0";
    }
}
