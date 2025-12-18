using DetonatorAgent.Services;
using System.Diagnostics;
using System.ComponentModel;
using System.IO.Compression;

namespace DetonatorAgent.Services;

public class WindowsExecutionServiceExec : IExecutionService {
    private readonly ILogger<WindowsExecutionServiceExec> _logger;
    private string _executableFilePath = "";
    public string ExecutionTypeName => "exec";

    // Tracking
    private int _lastProcessId = 0;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private Process? _lastProcess = null;
    private List<string> _cleanupFiles = new List<string>();

    public WindowsExecutionServiceExec(ILogger<WindowsExecutionServiceExec> logger) {
        _logger = logger;
    }

    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content, byte? xorKey = null) {
        // Write the give file to the filesystem
        // - with optional XOR decoding
        // - to the given filePath
        // - unzip it there if it's a .zip file
        try {
            _logger.LogInformation("Writing malware to: {FilePath}, xorkey: {XorKey}", filePath, xorKey.HasValue ? xorKey.Value.ToString() : "none");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            // Write file with optional XOR decoding
            await FileWriter.WriteAsync(filePath, content, xorKey);
            _logger.LogInformation("Successfully wrote malware to: {FilePath}", filePath);
            _executableFilePath = filePath;

            // Add the dropped file to cleanup list
            _cleanupFiles.Clear();
            _cleanupFiles.Add(filePath);

            // If it's a .zip file, extract it into the same directory
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            if (fileExtension == ".zip") {
                try {
                    var extractPath = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(extractPath)) {
                        _logger.LogInformation("Extracting zip file to: {ExtractPath}", extractPath);
                        
                        using (var archive = ZipFile.OpenRead(filePath)) {
                            // Get alphabetically first filename from zip
                            var firstEntry = archive.Entries
                                .Where(e => !string.IsNullOrEmpty(e.Name)) // Skip directory entries
                                .OrderBy(e => e.FullName)
                                .FirstOrDefault();
                            
                            if (firstEntry != null) {
                                _executableFilePath = Path.Combine(extractPath, firstEntry.FullName);
                                _logger.LogInformation("First file in zip (alphabetically): {FilePath}", _executableFilePath);
                            }
                            
                            // Extract all files
                            archive.ExtractToDirectory(extractPath, overwriteFiles: true);
                            
                            // Add all extracted files to cleanup list
                            _cleanupFiles.Clear();
                            foreach (var entry in archive.Entries) {
                                if (!string.IsNullOrEmpty(entry.Name)) {
                                    var extractedFilePath = Path.Combine(extractPath, entry.FullName);
                                    _cleanupFiles.Add(extractedFilePath);
                                }
                            }
                        }
                        _logger.LogInformation("Successfully extracted zip file");

                        // Now delete the original zip file
                        File.Delete(filePath);
                        _logger.LogInformation("Deleted original zip file: {FilePath}", filePath);
                    }
                }
                catch (Exception zipEx) {
                    _logger.LogError(zipEx, "Failed to extract zip file: {FilePath}", filePath);
                    // Don't fail the malware writing operation due to zip extraction failure
                }
            }

            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to write malware to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string? arguments = null) {
        try {
            _logger.LogInformation("Executing malware: {FilePath} with args: {Arguments}", _executableFilePath, arguments ?? "");

            // Check if the file is a DLL
            var fileExtension = Path.GetExtension(_executableFilePath).ToLowerInvariant();
            bool isDll = fileExtension == ".dll";

            ProcessStartInfo startInfo;
            
            if (isDll) {
                // Use rundll32.exe to execute DLL files
                _logger.LogInformation("Detected DLL file, using rundll32.exe to execute");

                // Build rundll32 command: rundll32.exe <dllpath>,<argument>
                if (string.IsNullOrEmpty(arguments)) {
                    // Error
                    _logger.LogError("No entry point specified for DLL execution (as argument)");
                    return (false, 0, "No entry point specified for DLL execution (as argument)");
                }
                string rundll32Args = $"\"{_executableFilePath}\",{arguments}";
                
                startInfo = new ProcessStartInfo {
                    FileName = "rundll32.exe",
                    Arguments = rundll32Args,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
            }
            else {
                startInfo = new ProcessStartInfo {
                    FileName = _executableFilePath,
                    Arguments = arguments ?? "",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
            }

            var process = Process.Start(startInfo);
            if (process == null) {
                _logger.LogError("Failed to start process: {FilePath}", _executableFilePath);
                return (false, 0, "Failed to start process");
            }

            // Try to get the PID - may not be available with UseShellExecute or DLLs
            int pid = -1;
            try {
                if (isDll) {
                    // For DLLs executed via rundll32, we don't have a meaningful PID
                    // The PID would be of rundll32.exe itself, not the DLL code
                    _logger.LogInformation("DLL execution: PID not available (rundll32.exe is the host process)");
                    pid = -1;
                }
                else {
                    pid = process.Id;
                }
            }
            catch {
                // If we can't get the PID, use a placeholder
                pid = -1;
            }

            // Clean up previous process if it exists
            _lastProcess?.Dispose();

            _lastProcessId = pid;
            _lastProcess = process;
            _lastStdout = string.Empty;
            _lastStderr = string.Empty;
            // Note: _lastExtractionPath and _lastMountedIsoPath are set in PrepareFileForExecutionAsync

            _logger.LogInformation("Process started successfully with PID: {Pid}", pid);

            // Note: With UseShellExecute = true, we cannot redirect stdout/stderr
            // The process may also spawn child processes that we don't track
            _ = Task.Run(async () => {
                try {
                    // Just wait for the process without reading output
                    await process.WaitForExitAsync();

                    _logger.LogInformation("Process {Pid} completed.", pid);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error waiting for process {Pid}", pid);
                }
            });

            // Return immediately without waiting for the process to exit
            await Task.CompletedTask;
            return (true, pid, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 225) // ERROR_OPERATION_ABORTED - virus detected
        {
            _logger.LogWarning("Malware execution blocked by antivirus: {FilePath} - Error code: {ErrorCode}", _executableFilePath, ex.NativeErrorCode);
            return (false, 0, "virus");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1234) // ERROR_VIRUS_INFECTED equivalent
        {
            _logger.LogWarning("Malware execution blocked by antivirus: {FilePath} - Error code: {ErrorCode}", _executableFilePath, ex.NativeErrorCode);
            return (false, 0, "virus");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing malware: {FilePath}", _executableFilePath);
            return (false, 0, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync() {
        try {
            Process? processToKill = null;
            int pidToKill = 0;

            if (_lastProcessId == 0 || _lastProcessId == -1) {
                _logger.LogWarning("No process to kill - no last execution found");
                return (true, "No process to kill - no last execution found");
            }

            pidToKill = _lastProcessId;
            processToKill = _lastProcess;

            _logger.LogInformation("Attempting to kill process with PID: {Pid}", pidToKill);

            try {
                // Try to kill using the stored process reference first
                if (processToKill != null && !processToKill.HasExited) {
                    processToKill.Kill();
                    await processToKill.WaitForExitAsync();
                    _logger.LogInformation("Successfully killed process with PID: {Pid} using process reference", pidToKill);
                }
                else {
                    // Fallback to getting process by ID
                    var process = Process.GetProcessById(pidToKill);
                    process.Kill();
                    await process.WaitForExitAsync();
                    _logger.LogInformation("Successfully killed process with PID: {Pid} using process ID", pidToKill);
                }

                _lastProcess?.Dispose();
                _lastProcess = null;

            }
            catch (ArgumentException) {
                _logger.LogWarning("Process with PID {Pid} not found - may have already exited", pidToKill);

                _lastProcess?.Dispose();
                _lastProcess = null;
            }
            catch (InvalidOperationException) {
                _logger.LogWarning("Process with PID {Pid} has already exited", pidToKill);

                _lastProcess?.Dispose();
                _lastProcess = null;
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error killing process with PID: {Pid}", _lastProcessId);
        }

        // Clean up tracked files
        // Sleep briefly to ensure files are not in use
        await Task.Delay(500);

        foreach (var fileToDelete in _cleanupFiles) {
            try {
                if (File.Exists(fileToDelete)) {
                    File.Delete(fileToDelete);
                    _logger.LogInformation("Deleted cleanup file: {FilePath}", fileToDelete);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to delete cleanup file: {FilePath}", fileToDelete);
            }
        }
        _cleanupFiles.Clear();

        return (true, "Cleanup done");
    }

    public async Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync() {
        await Task.CompletedTask; // For consistency with async pattern

        return (_lastProcessId, _lastStdout, _lastStderr);
    }

    private string? FindExecutableFile(string searchPath, string? executable_name) {
        var executableExtensions = new[] { ".exe", ".bat", ".com", ".lnk", ".dll" };

        if (!string.IsNullOrWhiteSpace(executable_name)) {
            // Use specified file
            var specifiedFilePath = Path.Combine(searchPath, executable_name);
            if (File.Exists(specifiedFilePath)) {
                _logger.LogInformation("Using specified file for execution: {ExecuteFile}", executable_name);
                return specifiedFilePath;
            }
            else {
                _logger.LogError("Specified file not found: {ExecuteFile}", executable_name);
                return null;
            }
        }
        else {
            // Find alphabetically first executable file
            var allFiles = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);
            var executableFiles = allFiles
                .Where(f => executableExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            if (executableFiles.Any()) {
                var fileToExecute = executableFiles.First();
                _logger.LogInformation("Selected alphabetically first executable: {FileName}", Path.GetFileName(fileToExecute));
                return fileToExecute;
            }
            else {
                _logger.LogError("No executable files found in {SearchPath}", searchPath);
                return null;
            }
        }
    }
}
