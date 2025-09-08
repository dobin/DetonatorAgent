using DetonatorAgent.Services;
using System.Diagnostics;

namespace DetonatorAgent.Services.Platform;

public class LinuxLogService : ILogService
{
    public async Task<string> GetLogsAsync()
    {
        // In a real implementation, this would read from /var/log files
        // or use journalctl for systemd logs
        await Task.Delay(100); // Simulate async operation
        
        return @"[2025-09-08 10:30:15] /var/log/syslog - INFO: Service detonator-agent started
[2025-09-08 10:30:20] /var/log/auth.log - WARNING: Failed login attempt from 192.168.1.100
[2025-09-08 10:30:25] /var/log/kern.log - INFO: USB device connected
[2025-09-08 10:30:30] /var/log/apache2/error.log - ERROR: Cannot connect to MySQL server
[2025-09-08 10:30:35] /var/log/syslog - INFO: Backup process completed successfully";
    }
}

public class LinuxEdrService : IEdrService
{
    private readonly ILogger<LinuxEdrService> _logger;

    public LinuxEdrService(ILogger<LinuxEdrService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCollectionAsync()
    {
        _logger.LogInformation("EDR collection not implemented for Linux platform");
        await Task.CompletedTask;
        return true; // Return true to not break the workflow
    }

    public async Task<bool> StopCollectionAsync()
    {
        _logger.LogInformation("EDR collection not implemented for Linux platform");
        await Task.CompletedTask;
        return true;
    }

    public async Task<string> GetLogsAsync()
    {
        await Task.CompletedTask;
        return "<Events>\n<!-- EDR collection not implemented for Linux platform -->\n</Events>";
    }

    public string GetEdrVersion()
    {
        return "Linux EDR Not Available";
    }

    public string GetPluginVersion()
    {
        return "1.0";
    }
}

public class LinuxExecutionService : IExecutionService
{
    private readonly ILogger<LinuxExecutionService> _logger;
    private readonly IEdrService _edrService;
    private static int _lastProcessId = 0; // Make static to persist across service instances

    public LinuxExecutionService(ILogger<LinuxExecutionService> logger, IEdrService edrService)
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
            
            // Set executable permissions on Linux
            var chmod = Process.Start("chmod", $"+x \"{filePath}\"");
            chmod?.WaitForExit();
            
            _logger.LogInformation("Successfully wrote malware to: {FilePath}", filePath);

            // Start EDR collection after writing malware
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
