using DetonatorAgent.Models;
using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;


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


    public EdrAlertsResponse GetEdrAlerts() {
        // Without this check, if StartCollection was not called, we have no start time
        // and we would return ALL events from the log, which can be a lot
        if (_startTime == default) {
            _logger.LogWarning("Defender Plugin: Error, StartCollection was not called before GetEdrAlerts");
            return new EdrAlertsResponse { Success = false, Alerts = new List<SubmissionAlert>(), IsDetected = false };
        }

        var rawLogs = GetDefenderEventsSince();
        var edrAlertsResponse = ParseDefenderEvents(rawLogs);
        return edrAlertsResponse;
    }


    [SupportedOSPlatform("windows")]
    private string GetDefenderEventsSince() {
        // Convert to the format expected by Event Log queries (ISO 8601 format with Z suffix)
        // Windows Event Log SystemTime is in UTC
        // Note that we ROUND DOWN with .000000000Z for start time
        //   - to catch events immediately following this
        //   - But not end time
        string startTimeStr = _startTime.ToString("yyyy-MM-ddTHH:mm:ss.000000000Z");
        string endTimeStr = _stopTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
        if (_stopTime == default) {
            // If no end time (after killing the process) is given, we just assume its until NOW
            endTimeStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
        }

        try {
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

    private EdrAlertsResponse ParseDefenderEvents(string edrData)
    {
        var response = new EdrAlertsResponse
        {
            Success = false,
            Alerts = new List<SubmissionAlert>(),
            IsDetected = false
        };

        if (string.IsNullOrWhiteSpace(edrData))
        {
            _logger.LogWarning("Defender Plugin: No XML data provided for parsing");
            return response;
        }

        try
        {
            // Remove namespace to simplify parsing (matching Python implementation)
            edrData = edrData.Replace("xmlns='http://schemas.microsoft.com/win/2004/08/events/event'", "");
            edrData = edrData.Replace("xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"", "");

            var xmlDoc = XDocument.Parse(edrData);
            var events = xmlDoc.Descendants("Event");

            foreach (var xmlEvent in events)
            {
                var parsedEvent = ParseWindowsEvent(xmlEvent);
                
                // Check if EventData exists and has a "Threat ID"
                if (!parsedEvent.TryGetValue("EventData", out var eventDataObj) || 
                    eventDataObj is not Dictionary<string, string> eventData ||
                    !eventData.ContainsKey("Threat ID"))
                {
                    continue;
                }

                // Extract fields
                string detectionId = eventData.GetValueOrDefault("Detection ID", "Unknown");
                string categoryName = eventData.GetValueOrDefault("Category Name", "Unknown");
                string detectionTimeStr = eventData.GetValueOrDefault("Detection Time", "Unknown");
                string severityName = eventData.GetValueOrDefault("Severity Name", "Unknown");
                string threatName = eventData.GetValueOrDefault("Threat Name", "Unknown");
                string sourceName = eventData.GetValueOrDefault("Source Name", "Unknown");

                // Parse detection time
                DateTime? detectionTime = null;
                if (detectionTimeStr != "Unknown")
                {
                    try
                    {
                        // Parse ISO 8601 format: 2025-07-04T14:55:37.511Z
                        detectionTime = DateTime.Parse(detectionTimeStr.Replace("Z", "+00:00"), 
                            null, System.Globalization.DateTimeStyles.RoundtripKind);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Defender Plugin: Failed to parse detection time '{DetectionTime}': {Error}", 
                            detectionTimeStr, ex.Message);
                    }
                }

                var alert = new SubmissionAlert
                {
                    Source = "Defender Local",
                    Raw = JsonSerializer.Serialize(eventData),
                    AlertId = detectionId,
                    Title = threatName,
                    Severity = severityName,
                    Category = categoryName,
                    DetectionSource = sourceName,
                    DetectedAt = detectionTime,
                    AdditionalData = new Dictionary<string, object>()
                };

                response.Alerts.Add(alert);
            }

            // Determine if detected
            if (edrData.Contains("Suspicious") || edrData.Contains("Threat ID"))
            {
                response.IsDetected = true;
            }

            response.Success = true;
            _logger.LogInformation("Defender Plugin: Successfully parsed {AlertCount} alerts", response.Alerts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Defender Plugin: Error parsing Windows Defender events");
        }

        return response;
    }

    private Dictionary<string, object> ParseWindowsEvent(XElement eventElement)
    {
        var data = new Dictionary<string, object>();

        // Parse System section
        var system = eventElement.Element("System");
        if (system != null)
        {
            var systemData = new Dictionary<string, object>();
            foreach (var child in system.Elements())
            {
                if (child.HasElements || child.Attributes().Any())
                {
                    // Handle elements with attributes or child elements
                    var attrs = child.Attributes().ToDictionary(a => a.Name.LocalName, a => (object)a.Value);
                    if (child.Value != null && !string.IsNullOrEmpty(child.Value))
                    {
                        attrs["_text"] = child.Value;
                    }
                    systemData[child.Name.LocalName] = attrs;
                }
                else
                {
                    systemData[child.Name.LocalName] = child.Value;
                }
            }
            data["System"] = systemData;
        }

        // Parse EventData section
        var eventData = eventElement.Element("EventData");
        if (eventData != null)
        {
            var eventDataDict = new Dictionary<string, string>();
            foreach (var dataElement in eventData.Elements("Data"))
            {
                var nameAttr = dataElement.Attribute("Name");
                if (nameAttr != null)
                {
                    eventDataDict[nameAttr.Value] = dataElement.Value ?? string.Empty;
                }
            }
            data["EventData"] = eventDataDict;
        }

        return data;
    }
}
