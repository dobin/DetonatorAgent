using DetonatorAgent.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoIt;

namespace DetonatorAgent.Services;

[SupportedOSPlatform("windows")]
public class WindowsExecutionServiceAutoit : IExecutionService {
    private readonly ILogger<WindowsExecutionServiceAutoit> _logger;
    private readonly IEdrService _edrService;
    private int _lastProcessId = 0;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private string? _lastExtractionPath = null;
    private string? _lastMountedIsoPath = null;
    private readonly object _processLock = new object();
    
    // AutoIt constants
    private const int SW_HIDE = 0;

    public WindowsExecutionServiceAutoit(ILogger<WindowsExecutionServiceAutoit> logger, IEdrService edrService) {
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
            _logger.LogInformation("Executing malware using AutoIt: {FilePath} with args: {Arguments}", filePath, arguments ?? "");

            // Use AutoIt to run the process
            int pid = AutoItX.Run(filePath, Path.GetDirectoryName(filePath) ?? "", SW_HIDE);

            if (pid == 0) {
                int errorCode = AutoItX.ErrorCode();
                _logger.LogError("Failed to start process using AutoIt: {FilePath}, Error code: {ErrorCode}", filePath, errorCode);
                
                // Check if it's an antivirus/security block
                if (errorCode == 1) {
                    return (false, 0, "virus");
                }
                
                return (false, 0, $"AutoIt failed to start process (error code: {errorCode})");
            }

            lock (_processLock) {
                _lastProcessId = pid;
                _lastStdout = string.Empty;
                _lastStderr = string.Empty;
                // Note: _lastExtractionPath and _lastMountedIsoPath are set in PrepareFileForExecutionAsync
            }

            var logPid = (int)pid;
            _logger.LogInformation("Process started successfully using AutoIt with PID: {Pid}", logPid);

            // Monitor the process in the background
            _ = Task.Run(async () => {
                try {
                    // Wait for the process to exit
                    while (AutoItX.ProcessExists(pid.ToString()) == 1) {
                        await Task.Delay(1000);
                    }

                    var completedPid = (int)pid;
                    _logger.LogInformation("Process {Pid} completed (AutoIt)", completedPid);
                }
                catch (Exception ex) {
                    var errorPid = (int)pid;
                    _logger.LogError(ex, "Error monitoring process {Pid} (AutoIt)", errorPid);
                }
            });

            // Return immediately without waiting for the process to exit
            await Task.CompletedTask;
            return (true, pid, null);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing malware using AutoIt: {FilePath}", filePath);
            
            // Check if error message suggests antivirus block
            if (ex.Message.Contains("virus", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase)) {
                return (false, 0, "virus");
            }
            
            return (false, 0, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync() {
        try {
            int pidToKill = 0;
            string? extractionPath = null;
            string? mountedIsoPath = null;

            lock (_processLock) {
                if (_lastProcessId == 0) {
                    _logger.LogWarning("No process to kill - no last execution found");
                    return (false, "No process to kill - no last execution found");
                }

                pidToKill = _lastProcessId;
                extractionPath = _lastExtractionPath;
                mountedIsoPath = _lastMountedIsoPath;
            }

            _logger.LogInformation("Attempting to kill process with PID using AutoIt: {Pid}", pidToKill);

            try {
                // Check if process exists
                if (AutoItX.ProcessExists(pidToKill.ToString()) == 1) {
                    // Kill the process using AutoIt
                    int result = AutoItX.ProcessClose(pidToKill.ToString());
                    
                    if (result == 0) {
                        int errorCode = AutoItX.ErrorCode();
                        _logger.LogWarning("AutoIt failed to kill process {Pid}, Error code: {ErrorCode}", pidToKill, errorCode);
                        return (false, $"Failed to kill process (AutoIt error: {errorCode})");
                    }

                    _logger.LogInformation("Successfully killed process with PID using AutoIt: {Pid}", pidToKill);
                    
                    // Wait a bit to ensure process is terminated
                    await Task.Delay(500);
                }
                else {
                    _logger.LogWarning("Process with PID {Pid} not found - may have already exited", pidToKill);
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
                        _logger.LogInformation("Unmounting ISO file using AutoIt: {IsoPath}", mountedIsoPath);

                        // Use AutoIt to run PowerShell command to dismount the ISO
                        var dismountCmd = $"powershell.exe -Command \"Dismount-DiskImage -ImagePath '{mountedIsoPath}'\"";
                        int dismountPid = AutoItX.Run(dismountCmd, "", SW_HIDE);
                        
                        if (dismountPid > 0) {
                            // Wait for dismount command to complete
                            while (AutoItX.ProcessExists(dismountPid.ToString()) == 1) {
                                await Task.Delay(100);
                            }
                            _logger.LogInformation("Successfully unmounted ISO file using AutoIt");
                        }
                        else {
                            _logger.LogWarning("Failed to start ISO unmount command using AutoIt");
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Failed to unmount ISO file: {IsoPath}", mountedIsoPath);
                    }
                }

                lock (_processLock) {
                    //_lastProcessId = 0; // Reset after successful kill
                    _lastExtractionPath = null;
                    _lastMountedIsoPath = null;
                }

                return (true, "Process killed successfully using AutoIt");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in kill process logic for PID: {Pid}", pidToKill);
                return (false, ex.Message);
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
        _logger.LogInformation("Detected ISO file: {FileName}, mounting it using AutoIt", Path.GetFileName(filePath));

        // Mount ISO using AutoIt by running it
        int mountPid = AutoItX.Run(filePath, Path.GetDirectoryName(filePath) ?? "", SW_HIDE);
        
        if (mountPid == 0) {
            int errorCode = AutoItX.ErrorCode();
            _logger.LogError("Failed to mount ISO using AutoIt. Error code: {ErrorCode}", errorCode);
            return (false, null, $"Failed to mount ISO (AutoIt error: {errorCode})");
        }

        _logger.LogInformation("ISO mount command executed using AutoIt");

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
        _logger.LogInformation("Detected archive file: {FileName}, extracting to temp directory using AutoIt", Path.GetFileName(filePath));

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

        // Extract ZIP file (using .NET since AutoIt doesn't have built-in ZIP extraction)
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
