using DetonatorAgent.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoIt;

namespace DetonatorAgent.Services;

[SupportedOSPlatform("windows")]
public class WindowsExecutionServiceAutoit : IExecutionService {
    private readonly ILogger<WindowsExecutionServiceAutoit> _logger;
    private readonly object _processLock = new object();
    private string _executableFilePath = "";

    // Tracking
    private int _lastProcessId = 0;
    private string? _lastExplorerWindowTitle = null;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;
    private string? _lastExtractionPath = null;
    private string? _lastMountedIsoPath = null;
    
    // AutoIt constants
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int SW_MAXIMIZE = 3;

    public string ExecutionTypeName => "autoit";

    public WindowsExecutionServiceAutoit(ILogger<WindowsExecutionServiceAutoit> logger) {
        _logger = logger;
    }

    
    public async Task<bool> _WriteMalwareAutoitAsync(string filePath, byte[] content, byte? xorKey = null) {
        try {
            _logger.LogInformation("Writing malware to: {FilePath}, xorkey: {XorKey}", filePath, xorKey.HasValue ? xorKey.Value.ToString() : "none");
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

            // First, write the content to a temporary file in the temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(filePath));
            await FileWriter.WriteAsync(tempPath, content, xorKey);
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

            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to write malware using AutoIt Explorer method to: {FilePath}", filePath);
            return false;
        }
    }


    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content, byte? xorKey = null) {
        try {
            _logger.LogInformation("Writing malware to: {FilePath}, xorkey: {XorKey}", filePath, xorKey.HasValue ? xorKey.Value.ToString() : "none");
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            await FileWriter.WriteAsync(filePath, content, xorKey);
            _logger.LogInformation("Successfully wrote malware to: {FilePath}", filePath);
            _executableFilePath = filePath;
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to write malware to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string? arguments = null) {
        try {
            _logger.LogInformation("Executing malware using AutoIt Explorer: {FilePath} with args: {Arguments}", _executableFilePath, arguments ?? "");

            var fileExtension = Path.GetExtension(_executableFilePath).ToLowerInvariant();
            int pid = 0;

            // Determine the type of file and use appropriate method
            if (fileExtension == ".zip" || fileExtension == ".iso") {
                // Archive/image file - open in explorer, navigate into it, and execute first file
                pid = await ExecuteArchiveViaExplorerAsync(_executableFilePath);
            }
            else {
                // Direct executable - open in explorer and double-click
                pid = await ExecuteFileViaExplorerAsync(_executableFilePath);
            }

            if (pid == 0) {
                int errorCode = AutoItX.ErrorCode();
                _logger.LogError("Failed to start process using AutoIt Explorer: {FilePath}, Error code: {ErrorCode}", _executableFilePath, errorCode);
                
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
            _logger.LogError(ex, "Error executing malware using AutoIt Explorer: {FilePath}", _executableFilePath);
            
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

    private async Task<int> ExecuteArchiveViaExplorerAsync(string filePath) {
        _logger.LogInformation("Opening archive/image file via Explorer to execute contents: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
            _logger.LogError("Invalid file path: {FilePath}", filePath);
            return 0;
        }

        // Open explorer with the archive/ISO file selected
        var explorerArgs = $"/select,\"{filePath}\"";
        int explorerPid = AutoItX.Run($"explorer.exe {explorerArgs}", directory, SW_SHOW);

        if (explorerPid == 0) {
            _logger.LogError("Failed to open explorer.exe");
            return 0;
        }

        _logger.LogInformation("Explorer opened with PID: {Pid}", explorerPid);

        // Wait for explorer window to appear
        await Task.Delay(1000);

        // Wait for the Explorer window to be active
        AutoItX.WinWait(directory, "", 5);
        AutoItX.WinActivate(directory);
        await Task.Delay(500);

        // Store the folder name for later cleanup
        var folderName = Path.GetFileName(directory);
        lock (_processLock) {
            _lastExplorerWindowTitle = folderName;
        }
        _logger.LogInformation("Stored Explorer window title for cleanup: {WindowTitle}", folderName);

        // Double-click to open the archive/ISO file (simulates human clicking to view contents)
        _logger.LogInformation("Double-clicking archive/ISO file to open: {FileName}", fileName);
        AutoItX.Send("{ENTER}");
        
        // Wait for the archive/ISO to open (Windows will either mount ISO or open ZIP in Explorer)
        await Task.Delay(2000);

        // For ZIP files, a new Explorer window opens showing contents
        // For ISO files, Windows mounts it and opens it in Explorer
        // Now we need to find and execute the first executable file in the opened location

        // Store ISO path for cleanup if it's an ISO
        if (Path.GetExtension(filePath).ToLowerInvariant() == ".iso") {
            lock (_processLock) {
                _lastMountedIsoPath = filePath;
            }
        }

        // For ZIP, store extraction path (the ZIP is opened virtually by Explorer)
        if (Path.GetExtension(filePath).ToLowerInvariant() == ".zip") {
            // ZIP files are opened virtually by Explorer, no actual extraction path
            // But we still need to track the window for cleanup
            _logger.LogInformation("ZIP file opened in Explorer window");
        }

        // Wait for the new window with archive contents to appear and become active
        await Task.Delay(1000);

        // Find the first executable file in the opened view
        // We'll look for files alphabetically and select the first .exe, .bat, .com
        // Navigate to the first file by typing its name or using arrow keys
        _logger.LogInformation("Navigating to first executable in archive contents");
        
        // Press Home to go to the first item in the list
        AutoItX.Send("{HOME}");
        await Task.Delay(300);

        // Look for an executable file by navigating through the list
        // We'll press Down arrow and check if we find an executable
        // For simplicity, we'll press Enter on the first item assuming it's executable
        // A more robust approach would scan the directory first, but this simulates human behavior
        
        // Try to find .exe files first by typing 'e' to jump to files starting with 'e'
        // Or just navigate to the first item and execute it
        bool foundExecutable = false;
        int maxAttempts = 20; // Try up to 20 files
        
        for (int i = 0; i < maxAttempts; i++) {
            // Get the currently selected item's name using clipboard
            AutoItX.Send("^c"); // Copy filename
            await Task.Delay(200);
            
            // We can't easily read clipboard from AutoIt, so we'll just try to execute
            // In a real scenario, we'd check the file extension
            // For now, simulate human behavior: look for first .exe by pressing Down until we find one
            
            // Simple approach: just execute the first item
            if (i == 0) {
                _logger.LogInformation("Attempting to execute first item in archive");
                AutoItX.Send("{ENTER}");
                foundExecutable = true;
                break;
            }
            
            await Task.Delay(300);
            AutoItX.Send("{DOWN}");
        }

        if (!foundExecutable) {
            _logger.LogWarning("Could not find executable in archive after {MaxAttempts} attempts", maxAttempts);
            return explorerPid;
        }

        // Wait for the file to start executing
        await Task.Delay(1000);

        // Try to find the PID of the started process
        // Since we don't know the exact process name, we'll return the explorer PID
        // and let the monitoring handle it
        _logger.LogInformation("Archive file execution initiated");

        // Try to find any new processes that started recently
        try {
            // Wait a bit more for process to fully start
            await Task.Delay(1000);
            
            // Get all processes and try to find newly started ones
            var allProcesses = System.Diagnostics.Process.GetProcesses();
            var recentProcesses = allProcesses
                .Where(p => {
                    try {
                        return (DateTime.Now - p.StartTime).TotalSeconds < 5;
                    }
                    catch {
                        return false;
                    }
                })
                .OrderByDescending(p => p.StartTime)
                .ToList();

            if (recentProcesses.Any()) {
                var newestProcess = recentProcesses.First();
                _logger.LogInformation("Found recently started process: {ProcessName} with PID: {Pid}", 
                    newestProcess.ProcessName, newestProcess.Id);
                return newestProcess.Id;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error finding recently started process");
        }

        _logger.LogWarning("Could not find PID for started process, returning explorer PID");
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

}
