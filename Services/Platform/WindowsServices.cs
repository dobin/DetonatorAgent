using DetonatorAgent.Services;
using System.Diagnostics;
using System.ComponentModel;

namespace DetonatorAgent.Services.Platform;

public class WindowsLogService : ILogService
{
    public async Task<string> GetLogsAsync()
    {
        // In a real implementation, this would read from Windows Event Log
        // using System.Diagnostics.EventLog
        await Task.Delay(100); // Simulate async operation
        
        return @"[2025-09-08 10:30:15] Windows Event Log - Information: Application started successfully
[2025-09-08 10:30:20] Windows Event Log - Warning: Memory usage above 80%
[2025-09-08 10:30:25] Windows Event Log - Information: Service checkpoint reached
[2025-09-08 10:30:30] Windows Event Log - Error: Failed to connect to database
[2025-09-08 10:30:35] Windows Event Log - Information: Recovery process initiated";
    }
}

public class WindowsExecutionService : IExecutionService
{
    private readonly ILogger<WindowsExecutionService> _logger;
    private readonly IEdrService _edrService;
    private static int _lastProcessId = 0; // Make static to persist across service instances

    public WindowsExecutionService(ILogger<WindowsExecutionService> logger, IEdrService edrService)
    {
        _logger = logger;
        _edrService = edrService;
    }

    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content)
    {
        try
        {
            _logger.LogInformation("Writing malware to: {FilePath}", filePath);
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(filePath, content);
            _logger.LogInformation("Successfully wrote malware to: {FilePath}", filePath);

            // Start EDR collection after writing malware (Windows only)
            try
            {
                var edrStartResult = await _edrService.StartCollectionAsync();
                if (edrStartResult)
                {
                    _logger.LogInformation("Started EDR collection after writing malware");
                }
                else
                {
                    _logger.LogWarning("Failed to start EDR collection after writing malware");
                }
            }
            catch (Exception edrEx)
            {
                _logger.LogError(edrEx, "Error starting EDR collection after writing malware");
                // Don't fail the malware writing operation due to EDR collection failure
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write malware to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null)
    {
        try
        {
            _logger.LogInformation("Executing malware: {FilePath} with args: {Arguments}", filePath, arguments ?? "");

            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = arguments ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start process: {FilePath}", filePath);
                return (false, 0, "Failed to start process");
            }

            var pid = process.Id;
            _lastProcessId = pid; // Store the last process ID for kill functionality
            _logger.LogInformation("Process started successfully with PID: {Pid}", pid);

            // Don't wait for the process to exit - just return the PID
            await Task.CompletedTask;
            return (true, pid, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1234) // ERROR_VIRUS_INFECTED equivalent
        {
            _logger.LogWarning("Malware execution blocked by antivirus: {FilePath}", filePath);
            return (false, 0, "virus");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing malware: {FilePath}", filePath);
            return (false, 0, ex.Message);
        }
    }

    public async Task<bool> KillLastExecutionAsync()
    {
        try
        {
            if (_lastProcessId == 0)
            {
                _logger.LogWarning("No process to kill - no last execution found");
                return false;
            }

            _logger.LogInformation("Attempting to kill process with PID: {Pid}", _lastProcessId);

            try
            {
                var process = Process.GetProcessById(_lastProcessId);
                process.Kill();
                await process.WaitForExitAsync();
                _logger.LogInformation("Successfully killed process with PID: {Pid}", _lastProcessId);
                _lastProcessId = 0; // Reset after successful kill
                return true;
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Process with PID {Pid} not found - may have already exited", _lastProcessId);
                _lastProcessId = 0; // Reset since process doesn't exist
                return true; // Consider this a success since the process is gone
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process with PID: {Pid}", _lastProcessId);
            return false;
        }
    }
}
