using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;


namespace DetonatorAgent.EdrPlugins;

[SupportedOSPlatform("windows")]
public class DefenderEdrPlugin : IEdrService {
    private readonly ILogger<DefenderEdrPlugin> _logger;

    private DateTime _startTime = default;
    private DateTime _stopTime = default;


    public DefenderEdrPlugin(ILogger<DefenderEdrPlugin> logger) {
        _logger = logger;
    }


    public bool StartCollection() {
        _startTime = DateTime.UtcNow;  // Use UTC to match Event Log SystemTime
        _stopTime = default;
        _logger.LogInformation("Defender Plugin: Started Windows Defender EDR log collection at {StartTime}", 
            _startTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        return true;
    }


    public bool StopCollection() {
        _stopTime = DateTime.UtcNow;  // Use UTC to match Event Log SystemTime
        _logger.LogInformation("Defender Plugin: Stopping Windows Defender EDR log collection at {StopTime}",
            _stopTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        return true;
    }


    public string GetLogs() {
        var logs = GetDefenderEventsSince();
        return logs;
    }


    [SupportedOSPlatform("windows")]
    private string GetDefenderEventsSince() {
        // Without this check, if StartCollection was not called, we have no start time
        // and we would return ALL events from the log, which can be a lot
        if (_startTime == default) {
            _logger.LogWarning("Defender Plugin: Error, StartCollection was not called before GetLogs");
            return "";
        }

        try {
            // Convert to the format expected by Event Log queries (ISO 8601 format with Z suffix)
            // Windows Event Log SystemTime is in UTC
            // Note that we ROUND DOWN with .000000000Z for start time 
            //      to catch events immediately following
            // But not end time
            string startTimeStr = _startTime.ToString("yyyy-MM-ddTHH:mm:ss.000000000Z");
            string endTimeStr = _stopTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
            if (_stopTime == default) {
                // If no end time (after killing the process) is given, we just assume its until NOW
                endTimeStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
            }

            // XPath query for filtering events by time range
            string query = $"*[System[TimeCreated[@SystemTime >= '{startTimeStr}' and @SystemTime <= '{endTimeStr}']]]";

            _logger.LogInformation("Defender Plugin: Querying Windows Defender events from {StartTime} to {EndTime}",
                startTimeStr, endTimeStr);
            _logger.LogDebug("Defender Plugin: With query string: {QueryString}", query);

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
                            _logger.LogWarning(ex, "Defender Plugin: Failed to convert event {EventId} to XML", eventRecord.Id);
                        }
                    }
                }

                _logger.LogInformation("Defender Plugin: Retrieved {EventCount} Windows Defender events", eventCount);
            }
            catch (EventLogNotFoundException) {
                _logger.LogWarning("Defender Plugin: Windows Defender Operational log not found. This might be expected on some systems.");
            }
            catch (UnauthorizedAccessException) {
                _logger.LogWarning("Defender Plugin: Access denied to Windows Defender Operational log. Administrator privileges may be required.");
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
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Defender");
        var query = new ObjectQuery("SELECT * FROM MSFT_MpComputerStatus");

        using var searcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject obj in searcher.Get())
        {
            return (
                obj["AMProductVersion"]?.ToString() + " - " + obj["AntivirusSignatureVersion"]?.ToString()
            );
        }

        return "Unknown";
    }
}
