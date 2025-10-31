using DetonatorAgent.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoIt;

namespace DetonatorAgent.Services;

[SupportedOSPlatform("windows")]
public class WindowsExecutionServiceAutoItFull : IExecutionService {
    private readonly ILogger<WindowsExecutionServiceAutoItFull> _logger;
    private readonly IEdrService _edrService;
    private int _lastProcessId = 0;
    private string? _lastExplorerWindowTitle = null;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private string? _lastExtractionPath = null;
    private string? _lastMountedIsoPath = null;
    private readonly object _processLock = new object();
    
    // AutoIt constants
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int SW_MAXIMIZE = 3;

    public string ExecutionTypeName => "autoitexplorer";

    public WindowsExecutionServiceAutoItFull(ILogger<WindowsExecutionServiceAutoItFull> logger, IEdrService edrService) {
        _logger = logger;
        _edrService = edrService;
    }

    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content, byte? xorKey = null) {
        try {
            _logger.LogInformation("Writing malware using AutoIt Explorer method to: {FilePath}", filePath);

            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
                _logger.LogError("Invalid file path: {FilePath}", filePath);
                return false;
            }

            // Ensure directory exists
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created directory: {Directory}", directory);
            }

            // XOR decode the content if xorKey is provided
            byte[] finalContent = content;
            if (xorKey.HasValue) {
                _logger.LogInformation("XOR decoding file with key: {XorKey}", xorKey.Value);
                finalContent = XorDecoder.Decode(content, xorKey.Value);
                _logger.LogInformation("XOR decoding completed. Original size: {OriginalSize}, Decoded size: {DecodedSize}", 
                    content.Length, finalContent.Length);
            }

            // First, write the content to a temporary file in the temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(filePath));
            await File.WriteAllBytesAsync(tempPath, finalContent);
            _logger.LogInformation("Wrote temporary file: {TempPath}", tempPath);

            // Open Explorer window to the destination directory
            var explorerArgs = $"\"{directory}\"";
            int explorerPid = AutoItX.Run($"explorer.exe {explorerArgs}", directory, SW_SHOW);

            if (explorerPid == 0) {
                _logger.LogError("Failed to open explorer.exe for destination directory");
                File.Delete(tempPath);
                return false;
            }

            _logger.LogInformation("Opened Explorer window for directory: {Directory} with PID: {Pid}", directory, explorerPid);

            // Wait for Explorer window to appear and become active
            await Task.Delay(1000);
            var folderName = Path.GetFileName(directory);
            AutoItX.WinWait(folderName, "", 10);
            AutoItX.WinActivate(folderName);
            await Task.Delay(500);

            // Open a second Explorer window with the temp file selected
            var tempExplorerArgs = $"/select,\"{tempPath}\"";
            int tempExplorerPid = AutoItX.Run($"explorer.exe {tempExplorerArgs}", Path.GetTempPath(), SW_SHOW);

            if (tempExplorerPid == 0) {
                _logger.LogError("Failed to open explorer.exe for temp file");
                AutoItX.WinClose(folderName);
                File.Delete(tempPath);
                return false;
            }

            _logger.LogInformation("Opened Explorer window with temp file selected");
            await Task.Delay(1000);

            // Copy the file using Ctrl+C
            _logger.LogInformation("Copying file with Ctrl+C");
            AutoItX.Send("^c");
            await Task.Delay(500);

            // Close the temp Explorer window
            AutoItX.Send("!{F4}");
            await Task.Delay(500);

            // Activate the destination Explorer window and paste
            AutoItX.WinActivate(folderName);
            await Task.Delay(500);
            
            _logger.LogInformation("Pasting file with Ctrl+V into destination directory");
            AutoItX.Send("^v");
            await Task.Delay(1000);

            // If the file already exists, handle the replace dialog
            // Look for a dialog that might ask to replace the file
            if (AutoItX.WinExists("Confirm File Replace") == 1) {
                _logger.LogInformation("File replace dialog detected, confirming replace");
                AutoItX.WinActivate("Confirm File Replace");
                await Task.Delay(300);
                AutoItX.Send("{ENTER}"); // Confirm replace
                await Task.Delay(500);
            }

            // Close the destination Explorer window
            AutoItX.WinClose(folderName);
            await Task.Delay(300);

            // Clean up temp file
            try {
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                    _logger.LogInformation("Deleted temporary file: {TempPath}", tempPath);
                }
            }
            catch (Exception cleanupEx) {
                _logger.LogWarning(cleanupEx, "Failed to delete temporary file: {TempPath}", tempPath);
            }

            // Verify the file was written
            if (File.Exists(filePath)) {
                _logger.LogInformation("Successfully wrote malware using AutoIt Explorer method to: {FilePath}", filePath);
            }
            else {
                _logger.LogError("File not found after AutoIt write operation: {FilePath}", filePath);
                return false;
            }

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
            _logger.LogError(ex, "Failed to write malware using AutoIt Explorer method to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null) {
        try {
            _logger.LogInformation("Executing malware using AutoIt Explorer: {FilePath} with args: {Arguments}", filePath, arguments ?? "");

            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            int pid = 0;

            // Determine the type of file and use appropriate method
            if (fileExtension == ".exe" || fileExtension == ".bat" || fileExtension == ".com") {
                // Direct executable - open in explorer and double-click
                pid = await ExecuteFileViaExplorerAsync(filePath);
            }
            else if (fileExtension == ".zip") {
                // ZIP file - handled in PrepareFileForExecutionAsync, but this shouldn't be called directly
                _logger.LogError("ZIP files should be prepared before execution");
                return (false, 0, "ZIP files must be prepared before execution");
            }
            else if (fileExtension == ".iso") {
                // ISO file - handled in PrepareFileForExecutionAsync, but this shouldn't be called directly
                _logger.LogError("ISO files should be prepared before execution");
                return (false, 0, "ISO files must be prepared before execution");
            }
            else {
                _logger.LogError("Unsupported file type: {Extension}", fileExtension);
                return (false, 0, $"Unsupported file type: {fileExtension}");
            }

            if (pid == 0) {
                int errorCode = AutoItX.ErrorCode();
                _logger.LogError("Failed to start process using AutoIt Explorer: {FilePath}, Error code: {ErrorCode}", filePath, errorCode);
                
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
            }

            var logPid = (int)pid;
            _logger.LogInformation("Process started successfully using AutoIt Explorer with PID: {Pid}", logPid);

            // Monitor the process in the background
            _ = Task.Run(async () => {
                try {
                    // Wait for the process to exit
                    while (AutoItX.ProcessExists(pid.ToString()) == 1) {
                        await Task.Delay(1000);
                    }

                    var completedPid = (int)pid;
                    _logger.LogInformation("Process {Pid} completed (AutoIt Explorer)", completedPid);
                }
                catch (Exception ex) {
                    var errorPid = (int)pid;
                    _logger.LogError(ex, "Error monitoring process {Pid} (AutoIt Explorer)", errorPid);
                }
            });

            return (true, pid, null);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing malware using AutoIt Explorer: {FilePath}", filePath);
            
            // Check if error message suggests antivirus block
            if (ex.Message.Contains("virus", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase)) {
                return (false, 0, "virus");
            }
            
            return (false, 0, ex.Message);
        }
    }

    private async Task<int> ExecuteFileViaExplorerAsync(string filePath) {
        _logger.LogInformation("Opening explorer.exe to execute file: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
            _logger.LogError("Invalid file path: {FilePath}", filePath);
            return 0;
        }

        // Open explorer with the file selected
        var explorerArgs = $"/select,\"{filePath}\"";
        int explorerPid = AutoItX.Run($"explorer.exe {explorerArgs}", directory, SW_SHOW);

        if (explorerPid == 0) {
            _logger.LogError("Failed to open explorer.exe");
            return 0;
        }

        _logger.LogInformation("Explorer opened with PID: {Pid}", explorerPid);

        // Wait for explorer window to appear
        await Task.Delay(500);

        // Wait for the Explorer window to be active (using the directory name as window title)
        AutoItX.WinWait(directory, "", 5);
        AutoItX.WinActivate(directory);
        await Task.Delay(500);

        // Store the folder name (not full path) as the window title for later cleanup
        // Explorer window titles show only the folder name, not the full path
        var folderName = Path.GetFileName(directory);
        lock (_processLock) {
            _lastExplorerWindowTitle = folderName;
        }
        _logger.LogInformation("Stored Explorer window title for cleanup: {WindowTitle} (from path: {FullPath})", folderName, directory);

        // Send Enter key to open the selected file
        _logger.LogInformation("Sending Enter key to open file: {FileName}", fileName);
        AutoItX.Send("{ENTER}");

        // Wait for the file to start executing
        await Task.Delay(500);

        // Try to find the PID of the started process using .NET Process class
        var processName = Path.GetFileNameWithoutExtension(filePath);
        
        try {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            if (processes.Length > 0) {
                // Get the most recently started process
                var newestProcess = processes.OrderByDescending(p => p.StartTime).FirstOrDefault();
                if (newestProcess != null) {
                    int foundPid = newestProcess.Id;
                    _logger.LogInformation("Found started process {ProcessName} with PID: {Pid}", processName, foundPid);
                    return foundPid;
                }
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error finding process {ProcessName}", processName);
        }

        _logger.LogWarning("Could not find PID for process {ProcessName}, returning explorer PID", processName);
        return explorerPid;
    }


    public async Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync() {
        try {
            int pidToKill = 0;
            string? explorerWindowTitle = null;
            string? extractionPath = null;
            string? mountedIsoPath = null;

            lock (_processLock) {
                if (_lastProcessId == 0) {
                    _logger.LogWarning("No process to kill - no last execution found");
                    return (false, "No process to kill - no last execution found");
                }

                pidToKill = _lastProcessId;
                explorerWindowTitle = _lastExplorerWindowTitle;
                extractionPath = _lastExtractionPath;
                mountedIsoPath = _lastMountedIsoPath;
            }

            _logger.LogInformation("Attempting to kill process with PID using AutoIt: {Pid}", pidToKill);

            try {
                // Try to kill the process using AutoIt - don't check if it exists first, just try to kill it
                int result = AutoItX.ProcessClose(pidToKill.ToString());
                
                if (result == 0) {
                    int errorCode = AutoItX.ErrorCode();
                    _logger.LogWarning("AutoIt failed to kill process {Pid}, Error code: {ErrorCode}. Trying taskkill as fallback.", pidToKill, errorCode);
                    
                    // Fallback to taskkill if AutoIt fails
                    try {
                        var startInfo = new System.Diagnostics.ProcessStartInfo {
                            FileName = "taskkill",
                            Arguments = $"/F /PID {pidToKill}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using var process = System.Diagnostics.Process.Start(startInfo);
                        if (process != null) {
                            await process.WaitForExitAsync();
                            var output = await process.StandardOutput.ReadToEndAsync();
                            var error = await process.StandardError.ReadToEndAsync();
                            
                            if (process.ExitCode == 0) {
                                _logger.LogInformation("Successfully killed process {Pid} using taskkill fallback", pidToKill);
                            } else {
                                _logger.LogError("taskkill failed for PID {Pid}. Output: {Output}, Error: {Error}", pidToKill, output, error);
                                return (false, $"Failed to kill process: {error}");
                            }
                        }
                    }
                    catch (Exception fallbackEx) {
                        _logger.LogError(fallbackEx, "Fallback taskkill also failed for PID {Pid}", pidToKill);
                        return (false, $"Failed to kill process with both AutoIt and taskkill: {fallbackEx.Message}");
                    }
                }
                else {
                    _logger.LogInformation("Successfully killed process with PID using AutoIt: {Pid}", pidToKill);
                }
                
                // Wait a bit to ensure process is terminated
                await Task.Delay(500);

                // Close the Explorer window if it was used
                if (!string.IsNullOrEmpty(explorerWindowTitle)) {
                    try {
                        _logger.LogInformation("Attempting to close Explorer window with title: {WindowTitle}", explorerWindowTitle);
                        
                        // Check if the window exists
                        int windowExists = AutoItX.WinExists(explorerWindowTitle);
                        
                        if (windowExists == 1) {
                            _logger.LogInformation("Explorer window found, attempting to close it");
                            
                            // Method 1: Try using WinClose (graceful close)
                            int closeResult = AutoItX.WinClose(explorerWindowTitle);
                            
                            if (closeResult == 1) {
                                _logger.LogInformation("WinClose sent to Explorer window");
                                await Task.Delay(500);
                                
                                // Check if window still exists
                                if (AutoItX.WinExists(explorerWindowTitle) == 0) {
                                    _logger.LogInformation("Successfully closed Explorer window using WinClose");
                                } else {
                                    // Method 2: Force close with WinKill
                                    _logger.LogInformation("Window still exists, using WinKill to force close");
                                    int killResult = AutoItX.WinKill(explorerWindowTitle);
                                    
                                    if (killResult == 1) {
                                        await Task.Delay(300);
                                        _logger.LogInformation("WinKill sent to Explorer window");
                                    } else {
                                        _logger.LogWarning("WinKill failed for Explorer window");
                                    }
                                }
                            } else {
                                _logger.LogWarning("WinClose failed for Explorer window, trying WinKill");
                                
                                // Try WinKill directly
                                int killResult = AutoItX.WinKill(explorerWindowTitle);
                                if (killResult == 1) {
                                    await Task.Delay(300);
                                    _logger.LogInformation("WinKill sent to Explorer window");
                                }
                            }
                        } else {
                            _logger.LogInformation("Explorer window with title '{WindowTitle}' not found - may have already been closed", explorerWindowTitle);
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogWarning(ex, "Error closing Explorer window with title: {WindowTitle}", explorerWindowTitle);
                    }
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

                        // Use AutoIt to run PowerShell command to dismount the ISO
                        var dismountCmd = $"powershell.exe -Command \"Dismount-DiskImage -ImagePath '{mountedIsoPath}'\"";
                        int dismountPid = AutoItX.Run(dismountCmd, "", SW_HIDE);
                        
                        if (dismountPid > 0) {
                            // Wait for dismount command to complete
                            while (AutoItX.ProcessExists(dismountPid.ToString()) == 1) {
                                await Task.Delay(100);
                            }
                            _logger.LogInformation("Successfully unmounted ISO file");
                        }
                        else {
                            _logger.LogWarning("Failed to start ISO unmount command");
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "Failed to unmount ISO file: {IsoPath}", mountedIsoPath);
                    }
                }

                lock (_processLock) {
                    _lastExtractionPath = null;
                    _lastMountedIsoPath = null;
                    _lastExplorerWindowTitle = null;
                }

                return (true, "Process killed successfully using AutoIt Explorer");
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

    private string? FindExecutableFile(string searchPath, string? executeFile) {
        var executableExtensions = new[] { ".exe", ".bat", ".com", ".lnk" };

        if (!string.IsNullOrWhiteSpace(executeFile)) {
            // Use specified file
            var specifiedFilePath = Path.Combine(searchPath, executeFile);
            if (File.Exists(specifiedFilePath)) {
                _logger.LogInformation("Using specified file for execution: {ExecuteFile}", executeFile);
                return specifiedFilePath;
            }
            else {
                _logger.LogError("Specified file not found: {ExecuteFile}", executeFile);
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

    private async Task<(bool Success, string? FilePath, string? ErrorMessage)> HandleIsoFileAsync(string filePath, string? executeFile) {
        _logger.LogInformation("Detected ISO file: {FileName}, will mount and execute via explorer.exe", Path.GetFileName(filePath));

        // Check if D: drive already exists
        if (Directory.Exists(@"D:\")) {
            _logger.LogWarning("D: drive already exists - may interfere with ISO mounting");
        }

        // Find the file to execute (will be used in ExecuteIsoViaExplorerAsync)
        var targetExecuteFile = executeFile ?? "*.exe";

        // Store the ISO path for later unmounting
        lock (_processLock) {
            _lastMountedIsoPath = filePath;
        }

        // We don't actually execute here - just validate and return info
        // The actual execution via explorer will happen in StartProcessAsync
        return (true, filePath, null);
    }

    private async Task<(bool Success, string? FilePath, string? ErrorMessage)> HandleZipFileAsync(string filePath, string? executeFile) {
        _logger.LogInformation("Detected ZIP file: {FileName}, will extract and execute via explorer.exe", Path.GetFileName(filePath));

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
        var fileToExecute = FindExecutableFile(tempPath, executeFile);

        if (fileToExecute == null) {
            var errorMsg = !string.IsNullOrWhiteSpace(executeFile)
                ? $"Specified file '{executeFile}' not found in archive"
                : "No executable files (.exe, .bat, .com, .lnk) found in archive";
            return (false, null, errorMsg);
        }

        return (true, fileToExecute, null);
    }

    public async Task<(bool Success, string? FilePath, string? ErrorMessage)> PrepareFileForExecutionAsync(string filePath, string? executeFile = null) {
        // Check if file is ZIP, RAR, or ISO and handle accordingly
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        if (fileExtension == ".iso") {
            return await HandleIsoFileAsync(filePath, executeFile);
        }
        else if (fileExtension == ".zip" || fileExtension == ".rar") {
            return await HandleZipFileAsync(filePath, executeFile);
        }

        // For regular executables, just return the original path
        return (true, filePath, null);
    }
}
