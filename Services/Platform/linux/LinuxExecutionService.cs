using DetonatorAgent.Services;
using System.Diagnostics;

namespace DetonatorAgent.Services.Platform.Linux;

public class LinuxExecutionService : IExecutionService
{
    private readonly ILogger<LinuxExecutionService> _logger;
    private readonly IEdrService _edrService;
    private int _lastProcessId = 0;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private Process? _lastProcess = null;
    private readonly object _processLock = new object();

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
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start process: {FilePath}", filePath);
                return (false, 0, "Failed to start process");
            }

            var pid = process.Id;
            
            lock (_processLock)
            {
                // Clean up previous process if it exists
                _lastProcess?.Dispose();
                
                _lastProcessId = pid;
                _lastProcess = process;
                _lastStdout = string.Empty;
                _lastStderr = string.Empty;
            }

            _logger.LogInformation("Process started successfully with PID: {Pid}", pid);

            // Start background task to collect stdout/stderr
            _ = Task.Run(async () =>
            {
                try
                {
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();

                    // Wait for the process to exit and capture output
                    await process.WaitForExitAsync();
                    
                    var stdout = await stdoutTask;
                    var stderr = await stderrTask;

                    lock (_processLock)
                    {
                        // Only update if this is still the current process
                        if (_lastProcessId == pid)
                        {
                            _lastStdout = stdout;
                            _lastStderr = stderr;
                        }
                    }

                    _logger.LogInformation("Process {Pid} completed. Stdout length: {StdoutLength}, Stderr length: {StderrLength}", 
                        pid, stdout.Length, stderr.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting output from process {Pid}", pid);
                    
                    lock (_processLock)
                    {
                        // Only update if this is still the current process
                        if (_lastProcessId == pid)
                        {
                            _lastStderr = $"Error collecting output: {ex.Message}";
                        }
                    }
                }
            });

            // Return immediately without waiting for the process to exit
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
            Process? processToKill = null;
            int pidToKill = 0;

            lock (_processLock)
            {
                if (_lastProcessId == 0)
                {
                    _logger.LogWarning("No process to kill - no last execution found");
                    return false;
                }

                pidToKill = _lastProcessId;
                processToKill = _lastProcess;
            }

            _logger.LogInformation("Attempting to kill process with PID: {Pid}", pidToKill);

            try
            {
                // Try to kill using the stored process reference first
                if (processToKill != null && !processToKill.HasExited)
                {
                    processToKill.Kill();
                    await processToKill.WaitForExitAsync();
                    _logger.LogInformation("Successfully killed process with PID: {Pid} using process reference", pidToKill);
                }
                else
                {
                    // Fallback to getting process by ID
                    var process = Process.GetProcessById(pidToKill);
                    process.Kill();
                    await process.WaitForExitAsync();
                    _logger.LogInformation("Successfully killed process with PID: {Pid} using process ID", pidToKill);
                }

                lock (_processLock)
                {
                    _lastProcessId = 0; // Reset after successful kill
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                }
                
                return true;
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Process with PID {Pid} not found - may have already exited", pidToKill);
                
                lock (_processLock)
                {
                    _lastProcessId = 0; // Reset since process doesn't exist
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                }
                
                return true; // Consider this a success since the process is gone
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Process with PID {Pid} has already exited", pidToKill);
                
                lock (_processLock)
                {
                    _lastProcessId = 0; // Reset since process has exited
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                }
                
                return true; // Consider this a success since the process is gone
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process with PID: {Pid}", _lastProcessId);
            return false;
        }
    }

    public async Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync()
    {
        await Task.CompletedTask; // For consistency with async pattern
        
        lock (_processLock)
        {
            return (_lastProcessId, _lastStdout, _lastStderr);
        }
    }
}
