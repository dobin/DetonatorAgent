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
public class FibratusEdrPlugin : IEdrService {
    private readonly ILogger<FibratusEdrPlugin> _logger;

    private DateTime _startTime = default;
    private DateTime _stopTime = default;


    public FibratusEdrPlugin(ILogger<FibratusEdrPlugin> logger) {
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
            return new EdrAlertsResponse { Success = false, Alerts = new List<SubmissionAlert>(), Detected = false };
        }

        var rawLogs = GetFibratusEventsSince();
        var edrAlertsResponse = ParseFibratusEvents(rawLogs);
        return edrAlertsResponse;
    }


    [SupportedOSPlatform("windows")]
    private string GetFibratusEventsSince() {
        // Fibratus logs are in Windows Logs/Application with source Fibratus

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
            // XPath query for filtering events by time range and source
            string query = $"*[System[Provider[@Name='Fibratus'] and TimeCreated[@SystemTime >= '{startTimeStr}' and @SystemTime <= '{endTimeStr}']]]";

            _logger.LogInformation("Fibratus Plugin: Querying Fibratus events from {StartTime} to {EndTime}",
                startTimeStr, endTimeStr);
            _logger.LogDebug("Fibratus Plugin: With query string: {QueryString}", query);

            var allEvents = new StringBuilder();
            allEvents.AppendLine("<Events>");

            try {
                var eventQuery = new EventLogQuery("Application", PathType.LogName, query);
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
                            _logger.LogWarning(ex, "Fibratus Plugin: Failed to convert event {EventId} to XML", eventRecord.Id);
                        }
                    }
                }

                _logger.LogInformation("Fibratus Plugin: Retrieved {EventCount} Fibratus events", eventCount);
            }
            catch (EventLogNotFoundException) {
                _logger.LogWarning("Fibratus Plugin: Application log not found. This might be expected on some systems.");
            }
            catch (UnauthorizedAccessException) {
                _logger.LogWarning("Fibratus Plugin: Access denied to Application log. Administrator privileges may be required.");
            }

            allEvents.AppendLine("</Events>");
            return allEvents.ToString();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error retrieving Fibratus events");
            return "<Events>\n<!-- Error retrieving events: " + ex.Message + " -->\n</Events>";
        }
    }

    
    public string GetEdrVersion() {
        // C:\Windows\System32>"C:\Program Files\Fibratus\Bin\fibratus.exe" version
        // ┌─────────────┬─────────────────────┐
        // │ Version     │ 2.4.0               │
        // │ Commit      │ 6e9efb83            │
        // │ Build date  │ 20-05-2025.17:13:06 │
        // ├─────────────┼─────────────────────┤
        // │ Go compiler │ go1.23.9            │
        // └─────────────┴─────────────────────┘

        try {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = @"C:\Program Files\Fibratus\Bin\fibratus.exe",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null) {
                _logger.LogWarning("Fibratus Plugin: Failed to start fibratus.exe");
                return "Unknown";
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                _logger.LogWarning("Fibratus Plugin: fibratus.exe exited with code {ExitCode}", process.ExitCode);
                return "Unknown";
            }

            // Parse the table output to extract version information
            string? version = null;
            string? commit = null;
            string? buildDate = null;

            var lines = output.Split('\n');
            foreach (var line in lines) {
                if (line.Contains("Version") && line.Contains("│")) {
                    var parts = line.Split('│');
                    if (parts.Length >= 3) {
                        version = parts[2].Trim();
                    }
                }
                else if (line.Contains("Commit") && line.Contains("│")) {
                    var parts = line.Split('│');
                    if (parts.Length >= 3) {
                        commit = parts[2].Trim();
                    }
                }
                else if (line.Contains("Build date") && line.Contains("│")) {
                    var parts = line.Split('│');
                    if (parts.Length >= 3) {
                        buildDate = parts[2].Trim();
                    }
                }
            }

            if (version != null) {
                var versionInfo = version;
                if (commit != null) {
                    versionInfo += $" ({commit}";
                    if (buildDate != null) {
                        versionInfo += $", {buildDate}";
                    }
                    versionInfo += ")";
                }
                return versionInfo;
            }

            return "Unknown";
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Fibratus Plugin: Error getting Fibratus version");
            return "Unknown";
        }
    }

    private EdrAlertsResponse ParseFibratusEvents(string edrData)
    {
        var response = new EdrAlertsResponse
        {
            Success = false,
            Alerts = new List<SubmissionAlert>(),
            Detected = false
        };

        if (string.IsNullOrWhiteSpace(edrData))
        {
            _logger.LogWarning("Fibratus Plugin: No XML data provided for parsing");
            return response;
        }

        try
        {
            // Remove namespace to simplify parsing
            edrData = edrData.Replace("xmlns='http://schemas.microsoft.com/win/2004/08/events/event'", "");
            edrData = edrData.Replace("xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"", "");

            var xmlDoc = XDocument.Parse(edrData);
            var events = xmlDoc.Descendants("Event");

            foreach (var xmlEvent in events)
            {
                var parsedEvent = ParseWindowsEvent(xmlEvent);
                
                // Check if EventData exists
                if (!parsedEvent.TryGetValue("EventData", out var eventDataObj) || 
                    eventDataObj is not Dictionary<string, string> eventData ||
                    !eventData.ContainsKey("Data"))
                {
                    continue;
                }

                // Get the full data text from Fibratus event
                string dataText = eventData["Data"];
                
                // Fibratus alerts typically contain "Severity:" in the event data
                // or start with phrases like "Suspicious" or "Credential discovery"
                if (!dataText.Contains("Severity:", StringComparison.OrdinalIgnoreCase))
                {
                    // Not a Fibratus alert event
                    continue;
                }

                // Extract alert title (first line or sentence before "Severity:")
                string threatName = "Unknown";
                int severityIndex = dataText.IndexOf("Severity:", StringComparison.OrdinalIgnoreCase);
                if (severityIndex > 0)
                {
                    threatName = dataText.Substring(0, severityIndex).Trim();
                    // If there's a newline or description, take only the first part
                    int newlineIndex = threatName.IndexOf('\n');
                    if (newlineIndex > 0)
                    {
                        threatName = threatName.Substring(0, newlineIndex).Trim();
                    }
                }
                else if (dataText.StartsWith("Suspicious", StringComparison.OrdinalIgnoreCase))
                {
                    // Take the first line for suspicious alerts
                    int newlineIndex = dataText.IndexOf('\n');
                    threatName = newlineIndex > 0 ? dataText.Substring(0, newlineIndex).Trim() : dataText;
                }

                // Extract severity
                string severityName = "Unknown";
                var severityMatch = System.Text.RegularExpressions.Regex.Match(dataText, @"Severity:\s*(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (severityMatch.Success)
                {
                    severityName = severityMatch.Groups[1].Value;
                }

                // Extract detection time from System/TimeCreated
                DateTime? detectionTime = null;
                if (parsedEvent.TryGetValue("System", out var systemObj) && 
                    systemObj is Dictionary<string, object> systemData &&
                    systemData.TryGetValue("TimeCreated", out var timeCreatedObj))
                {
                    if (timeCreatedObj is Dictionary<string, object> timeCreated &&
                        timeCreated.TryGetValue("SystemTime", out var systemTimeObj) &&
                        systemTimeObj is string systemTimeStr)
                    {
                        try
                        {
                            detectionTime = DateTime.Parse(systemTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Fibratus Plugin: Failed to parse detection time '{DetectionTime}': {Error}", 
                                systemTimeStr, ex.Message);
                        }
                    }
                }

                // Extract detection ID from EventRecordID
                string detectionId = "Unknown";
                if (parsedEvent.TryGetValue("System", out var systemObj2) && 
                    systemObj2 is Dictionary<string, object> systemData2 &&
                    systemData2.TryGetValue("EventRecordID", out var recordIdObj))
                {
                    detectionId = recordIdObj.ToString() ?? "Unknown";
                }

                // Extract category from the event data (look for "Category: process" etc.)
                string categoryName = "Unknown";
                var categoryMatch = System.Text.RegularExpressions.Regex.Match(dataText, @"Category:\s*(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (categoryMatch.Success)
                {
                    categoryName = categoryMatch.Groups[1].Value;
                }

                string sourceName = "Fibratus";

                var alert = new SubmissionAlert
                {
                    Source = "Fibratus",
                    Raw = dataText,
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
            if (response.Alerts.Count > 0)
            {
                response.Detected = true;
            }

            response.Success = true;
            _logger.LogInformation("Fibratus Plugin: Successfully parsed {AlertCount} alerts", response.Alerts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fibratus Plugin: Error parsing Fibratus events");
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
            int unnamedIndex = 0;
            foreach (var dataElement in eventData.Elements("Data"))
            {
                var nameAttr = dataElement.Attribute("Name");
                if (nameAttr != null)
                {
                    eventDataDict[nameAttr.Value] = dataElement.Value ?? string.Empty;
                }
                else
                {
                    // For unnamed Data elements, use "Data" as the key (or "Data0", "Data1", etc. for multiple)
                    string key = unnamedIndex == 0 ? "Data" : $"Data{unnamedIndex}";
                    eventDataDict[key] = dataElement.Value ?? string.Empty;
                    unnamedIndex++;
                }
            }
            data["EventData"] = eventDataDict;
        }

        return data;
    }
}
