using DetonatorAgent.Services;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;

namespace DetonatorAgent.Services.Platform.Windows;

[SupportedOSPlatform("windows")]
public class WindowsDefenderEdrService : IEdrService
{
    private readonly ILogger<WindowsDefenderEdrService> _logger;
    private DateTime _startTime;
    private string _collectedLogs = string.Empty;
    private readonly object _lockObject = new object();

    public WindowsDefenderEdrService(ILogger<WindowsDefenderEdrService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync()
    {
        try
        {
            lock (_lockObject)
            {
                _startTime = DateTime.UtcNow;
                _collectedLogs = string.Empty;
            }
            
            _logger.LogInformation("Started Windows Defender EDR log collection at {StartTime}", _startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Windows Defender EDR log collection");
            return false;
        }
    }

    public async Task<bool> StopCollectionAsync()
    {
        try
        {
            DateTime startTime;
            DateTime stopTime;
            lock (_lockObject)
            {
                startTime = _startTime;
                stopTime = DateTime.UtcNow;
            }

            _logger.LogInformation("Stopping Windows Defender EDR log collection. Collecting events from {StartTime} to {StopTime}", 
                startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), 
                stopTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

            var logs = await GetDefenderEventsSinceAsync(startTime, stopTime);
            
            lock (_lockObject)
            {
                _collectedLogs = logs;
            }

            _logger.LogInformation("Collected {LogLength} characters of Windows Defender events", logs.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Windows Defender EDR log collection");
            return false;
        }
    }

    public async Task<string> GetLogsAsync()
    {
        await Task.CompletedTask;
        lock (_lockObject)
        {
            return _collectedLogs;
        }
    }

    public string GetEdrVersion()
    {
        return "Windows Defender 1.0";
    }

    public string GetPluginVersion()
    {
        return "1.0";
    }

    [SupportedOSPlatform("windows")]
    private async Task<string> GetDefenderEventsSinceAsync(DateTime startTime, DateTime endTime)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Convert to the format expected by Event Log queries (ISO 8601 format)
                // Windows Event Log expects UTC time in this specific format
                string startTimeStr = startTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");
                string endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff000Z");

                // XPath query for filtering events by time range
                string query = $"*[System[TimeCreated[@SystemTime >= '{startTimeStr}' and @SystemTime <= '{endTimeStr}']]]";

                _logger.LogDebug("Querying Windows Defender events from {StartTime} to {EndTime} with query: {Query}", 
                    startTimeStr, endTimeStr, query);

                var allEvents = new StringBuilder();
                allEvents.AppendLine("<Events>");

                try
                {
                    var eventQuery = new EventLogQuery("Microsoft-Windows-Windows Defender/Operational", PathType.LogName, query);
                    using var eventReader = new EventLogReader(eventQuery);

                    EventRecord? eventRecord;
                    int eventCount = 0;

                    while ((eventRecord = eventReader.ReadEvent()) != null)
                    {
                        using (eventRecord)
                        {
                            try
                            {
                                // Convert the event to XML format
                                string eventXml = eventRecord.ToXml();
                                allEvents.AppendLine(eventXml);
                                eventCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to convert event {EventId} to XML", eventRecord.Id);
                            }
                        }
                    }

                    _logger.LogInformation("Retrieved {EventCount} Windows Defender events", eventCount);
                }
                catch (EventLogNotFoundException)
                {
                    _logger.LogWarning("Windows Defender Operational log not found. This might be expected on some systems.");
                    allEvents.AppendLine("<!-- Windows Defender Operational log not found -->");
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning("Access denied to Windows Defender Operational log. Administrator privileges may be required.");
                    allEvents.AppendLine("<!-- Access denied to Windows Defender Operational log -->");
                }

                allEvents.AppendLine("</Events>");
                return allEvents.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Windows Defender events");
                return "<Events>\n<!-- Error retrieving events: " + ex.Message + " -->\n</Events>";
            }
        });
    }
}
