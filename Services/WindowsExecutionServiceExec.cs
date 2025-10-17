using DetonatorAgent.Services;
using System.Diagnostics;
using System.ComponentModel;
using System.IO.Compression;

namespace DetonatorAgent.Services;

public class WindowsExecutionServiceExec : IExecutionService {
    private readonly ILogger<WindowsExecutionServiceExec> _logger;
    private readonly IEdrService _edrService;
    private int _lastProcessId = 0;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private Process? _lastProcess = null;
    private string? _lastExtractionPath = null;
    private string? _lastMountedIsoPath = null;
    private readonly object _processLock = new object();

    public WindowsExecutionServiceExec(ILogger<WindowsExecutionServiceExec> logger, IEdrService edrService) {
        _logger = logger;
        _edrService = edrService;
    }

    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content) {
        try {
            _logger.LogInformation("Writing malware to: {FilePath}", filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(filePath, content);
            _logger.LogInformation("Successfully wrote malware to: {FilePath}", filePath);

            // Start EDR collection after writing malware (Windows only)
            try {
                var edrStartResult = await _edrService.StartCollectionAsync();
                if (edrStartResult) {
                    _logger.LogInformation("Started EDR collection after writing malware");
                }
                else {
                    _logger.LogWarning("Failed to start EDR collection after writing malware");
                }
            }
            catch (Exception edrEx) {
                _logger.LogError(edrEx, "Error starting EDR collection after writing malware");
                // Don't fail the malware writing operation due to EDR collection failure
            }

            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to write malware to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null) {
        try {
            _logger.LogInformation("Executing malware: {FilePath} with args: {Arguments}", filePath, arguments ?? "");

            var startInfo = new ProcessStartInfo {
                FileName = filePath,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process == null) {
                _logger.LogError("Failed to start process: {FilePath}", filePath);
                return (false, 0, "Failed to start process");
            }

            // Try to get the PID - may not be available with UseShellExecute
            int pid = 0;
            try {
                pid = process.Id;
            }
            catch {
                // If we can't get the PID, use a placeholder
                pid = -1;
            }

            lock (_processLock) {
                // Clean up previous process if it exists
                _lastProcess?.Dispose();

                _lastProcessId = pid;
                _lastProcess = process;
                _lastStdout = string.Empty;
                _lastStderr = string.Empty;
                // Note: _lastExtractionPath and _lastMountedIsoPath are set in PrepareFileForExecutionAsync
            }

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
            _logger.LogWarning("Malware execution blocked by antivirus: {FilePath} - Error code: {ErrorCode}", filePath, ex.NativeErrorCode);
            return (false, 0, "virus");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1234) // ERROR_VIRUS_INFECTED equivalent
        {
            _logger.LogWarning("Malware execution blocked by antivirus: {FilePath} - Error code: {ErrorCode}", filePath, ex.NativeErrorCode);
            return (false, 0, "virus");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing malware: {FilePath}", filePath);
            return (false, 0, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync() {
        try {
            Process? processToKill = null;
            int pidToKill = 0;
            string? extractionPath = null;
            string? mountedIsoPath = null;

            lock (_processLock) {
                if (_lastProcessId == 0) {
                    _logger.LogWarning("No process to kill - no last execution found");
                    return (false, "No process to kill - no last execution found");
                }

                pidToKill = _lastProcessId;
                processToKill = _lastProcess;
                extractionPath = _lastExtractionPath;
                mountedIsoPath = _lastMountedIsoPath;
            }

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

                // Clean up extracted directory if exists
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath)) {
                    try {
                        _logger.LogInformation("Deleting extracted directory: {ExtractionPath}", extractionPath);
                        Directory.Delete(extractionPath, recursive: true);
                        _logger.LogInformation("Successfully deleted extracted directory");
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Failed to delete extracted directory: {ExtractionPath}", extractionPath);
                    }
                }

                // Unmount ISO if exists
                if (!string.IsNullOrEmpty(mountedIsoPath) && File.Exists(mountedIsoPath)) {
                    try {
                        _logger.LogInformation("Unmounting ISO file: {IsoPath}", mountedIsoPath);

                        // Use PowerShell to dismount the ISO
                        var dismountProcess = new Process {
                            StartInfo = new ProcessStartInfo {
                                FileName = "powershell.exe",
                                Arguments = $"-Command \"Dismount-DiskImage -ImagePath '{mountedIsoPath}'\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            }
                        };

                        dismountProcess.Start();
                        await dismountProcess.WaitForExitAsync();

                        if (dismountProcess.ExitCode == 0) {
                            _logger.LogInformation("Successfully unmounted ISO file");
                        }
                        else {
                            var errorOutput = await dismountProcess.StandardError.ReadToEndAsync();
                            _logger.LogWarning("Failed to unmount ISO file. Exit code: {ExitCode}, Error: {Error}",
                                dismountProcess.ExitCode, errorOutput);
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Failed to unmount ISO file: {IsoPath}", mountedIsoPath);
                    }
                }

                lock (_processLock) {
                    //_lastProcessId = 0; // Reset after successful kill
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                    _lastExtractionPath = null;
                    _lastMountedIsoPath = null;
                }

                return (true, "Process killed successfully");
            }
            catch (ArgumentException) {
                _logger.LogWarning("Process with PID {Pid} not found - may have already exited", pidToKill);

                lock (_processLock) {
                    //_lastProcessId = 0; // Reset since process doesn't exist
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                    _lastExtractionPath = null;
                    _lastMountedIsoPath = null;
                }

                return (true, "Process not found"); // Consider this a success since the process is gone
            }
            catch (InvalidOperationException) {
                _logger.LogWarning("Process with PID {Pid} has already exited", pidToKill);

                lock (_processLock) {
                    //_lastProcessId = 0; // Reset since process has exited
                    _lastProcess?.Dispose();
                    _lastProcess = null;
                    _lastExtractionPath = null;
                    _lastMountedIsoPath = null;
                }

                return (true, "Process already exited"); // Consider this a success since the process is gone
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error killing process with PID: {Pid}", _lastProcessId);
            return (false, ex.Message);
        }
    }

    public async Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync() {
        await Task.CompletedTask; // For consistency with async pattern

        lock (_processLock) {
            return (_lastProcessId, _lastStdout, _lastStderr);
        }
    }

    private string? FindExecutableFile(string searchPath, string? executable_name) {
        var executableExtensions = new[] { ".exe", ".bat", ".com", ".lnk" };

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

    private async Task<(bool Success, string? FilePath, string? ErrorMessage)> HandleIsoFileAsync(string filePath, string? executable_name) {
        _logger.LogInformation("Detected ISO file: {FileName}, mounting it", Path.GetFileName(filePath));

        // Mount ISO by starting it directly (Windows will mount it automatically)
        var mountProcess = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = filePath,
                UseShellExecute = true,
                CreateNoWindow = true
            }
        };

        mountProcess.Start();
        _logger.LogInformation("ISO mount command executed");

        // Wait for Windows to mount the ISO
        await Task.Delay(3000);

        // Check if D: drive exists (common mount point)
        if (!Directory.Exists(@"D:\")) {
            _logger.LogError("D: drive not found after mounting ISO");
            return (false, null, "Failed to mount ISO or D: drive not accessible");
        }

        _logger.LogInformation("ISO mounted to D: drive");

        // Store the ISO path for later unmounting
        lock (_processLock) {
            _lastMountedIsoPath = filePath;
        }

        // Find the file to execute on D: drive
        var fileToExecute = FindExecutableFile(@"D:\", executable_name);

        if (fileToExecute == null) {
            var errorMsg = !string.IsNullOrWhiteSpace(executable_name)
                ? $"Specified file '{executable_name}' not found on mounted ISO"
                : "No executable files (.exe, .bat, .com, .lnk) found on mounted ISO";
            return (false, null, errorMsg);
        }

        return (true, fileToExecute, null);
    }

    private async Task<(bool Success, string? FilePath, string? ErrorMessage)> HandleZipFileAsync(string filePath, string? executable_name) {
        _logger.LogInformation("Detected archive file: {FileName}, extracting to temp directory", Path.GetFileName(filePath));

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        // Check for RAR files (not supported)
        if (fileExtension == ".rar") {
            _logger.LogError("RAR files are not yet supported");
            return (false, null, "RAR files are not yet supported. Please use ZIP files instead.");
        }

        // Create extraction directory in user's temp folder
        var tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);
        _logger.LogInformation("Created extraction directory: {TempPath}", tempPath);

        // Extract ZIP file
        using (var zip = ZipFile.OpenRead(filePath)) {
            zip.ExtractToDirectory(tempPath, overwriteFiles: true);
        }
        _logger.LogInformation("Successfully extracted ZIP file to: {TempPath}", tempPath);

        // Store the extraction path for later cleanup
        lock (_processLock) {
            _lastExtractionPath = tempPath;
        }

        // Find the file to execute
        var fileToExecute = FindExecutableFile(tempPath, executable_name);

        if (fileToExecute == null) {
            var errorMsg = !string.IsNullOrWhiteSpace(executable_name)
                ? $"Specified file '{executable_name}' not found in archive"
                : "No executable files (.exe, .bat, .com, .lnk) found in archive";
            return (false, null, errorMsg);
        }

        return (true, fileToExecute, null);
    }

    public async Task<(bool Success, string? FilePath, string? ErrorMessage)> PrepareFileForExecutionAsync(string filePath, string? executable_name = null) {
        // Check if file is ZIP, RAR, or ISO and handle accordingly
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        if (fileExtension == ".iso") {
            return await HandleIsoFileAsync(filePath, executable_name);
        }
        else if (fileExtension == ".zip" || fileExtension == ".rar") {
            return await HandleZipFileAsync(filePath, executable_name);
        }

        // For regular executables, just return the original path
        return (true, filePath, null);
    }
}
