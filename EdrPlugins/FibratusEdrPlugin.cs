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

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            Task.WhenAll(stdoutTask, stderrTask).GetAwaiter().GetResult();
            process.WaitForExit();
            string output = stdoutTask.Result;

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
                    _logger.LogDebug("Fibratus Plugin: Skipping event with no EventData/Data field");
                    continue;
                }

                string dataText = eventData["Data"];

                // Fibratus now emits JSON alert payloads; skip non-JSON entries
                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(dataText);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Fibratus Plugin: Failed to parse event data as JSON. Data: {DataText}",
                        dataText.Length > 200 ? dataText.Substring(0, 200) + "..." : dataText);
                    continue;
                }

                using (jsonDoc)
                {
                    var root = jsonDoc.RootElement;

                    // Must have a "title" field to be considered an alert
                    if (!root.TryGetProperty("title", out var titleProp))
                    {
                        _logger.LogDebug("Fibratus Plugin: Skipping JSON event with no 'title' field");
                        continue;
                    }

                    string alertId = root.TryGetProperty("id", out var idProp)
                        ? idProp.GetString() ?? "Unknown" : "Unknown";
                    string title = titleProp.GetString() ?? "Unknown";
                    string severity = root.TryGetProperty("severity", out var severityProp)
                        ? severityProp.GetString() ?? "Unknown" : "Unknown";

                    // Extract category and timestamp from the first event entry
                    string category = "Unknown";
                    DateTime? detectionTime = null;
                    if (root.TryGetProperty("events", out var eventsProp) &&
                        eventsProp.ValueKind == JsonValueKind.Array &&
                        eventsProp.GetArrayLength() > 0)
                    {
                        var firstEvent = eventsProp[0];
                        if (firstEvent.TryGetProperty("category", out var categoryProp))
                            category = categoryProp.GetString() ?? "Unknown";

                        if (firstEvent.TryGetProperty("timestamp", out var tsProp))
                        {
                            string? tsStr = tsProp.GetString();
                            if (tsStr != null)
                            {
                                try
                                {
                                    detectionTime = DateTime.Parse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Fibratus Plugin: Failed to parse detection time '{DetectionTime}': {Error}",
                                        tsStr, ex.Message);
                                }
                            }
                        }
                    }

                    // Fallback to Windows Event Log SystemTime when no timestamp in JSON
                    if (detectionTime == null &&
                        parsedEvent.TryGetValue("System", out var systemObj) &&
                        systemObj is Dictionary<string, object> systemData &&
                        systemData.TryGetValue("TimeCreated", out var timeCreatedObj) &&
                        timeCreatedObj is Dictionary<string, object> timeCreated &&
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

                    // Collect MITRE labels as additional data
                    var additionalData = new Dictionary<string, object>();
                    if (root.TryGetProperty("labels", out var labelsProp) &&
                        labelsProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var label in labelsProp.EnumerateObject())
                            additionalData[label.Name] = label.Value.GetString() ?? string.Empty;
                    }

                    var alert = new SubmissionAlert
                    {
                        Source = "Fibratus",
                        Raw = dataText,
                        AlertId = alertId,
                        Title = title,
                        Severity = severity,
                        Category = category,
                        DetectionSource = "Fibratus",
                        DetectedAt = detectionTime,
                        AdditionalData = additionalData
                    };

                    response.Alerts.Add(alert);
                }
            }

            if (response.Alerts.Count > 0)
                response.Detected = true;

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
