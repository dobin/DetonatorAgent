using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace DetonatorAgent.Services;

/// <summary>
/// Executes a directly dropped executable through an Explorer window automated with UIA3.
/// This first FlaUI implementation intentionally does not handle ZIP/ISO containers.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsExecutionServiceFlaUi : IExecutionService
{
    private const uint WmClose = 0x0010;
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly ILogger<IExecutionService> _logger;
    private readonly IEdrService? _edrService;
    private readonly object _stateLock = new();

    private string _droppedFilePath = string.Empty;
    private int _lastProcessId;
    private nint _lastExplorerWindowHandle;
    private string _lastStdout = string.Empty;
    private string _lastStderr = string.Empty;

    public string ExecutionTypeName => "flaui";

    public WindowsExecutionServiceFlaUi(ILogger<IExecutionService> logger, IEdrService? edrService = null)
    {
        _logger = logger;
        _edrService = edrService;
    }

    public FileWriteResult WriteFile(string filePath, byte[] content, byte? xorKey = null)
    {
        lock (_stateLock)
        {
            _droppedFilePath = string.Empty;
            _lastProcessId = 0;
            _lastExplorerWindowHandle = 0;
            _lastStdout = string.Empty;
            _lastStderr = string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var result = FileWriter.Write(filePath, content, xorKey);
            if (result.IsWritten)
            {
                lock (_stateLock)
                {
                    _droppedFilePath = filePath;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file for FlaUI execution: {FilePath}", filePath);
            return new FileWriteResult(FileWriteStatus.Failed, ex.Message);
        }
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string? arguments = null)
    {
        string filePath;
        lock (_stateLock)
        {
            filePath = _droppedFilePath;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return (false, 0, "No dropped file is available for FlaUI execution");
        }

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return (false, 0, "The FlaUI Explorer mode does not support executable arguments yet");
        }

        var extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return (false, 0, $"The FlaUI implementation currently supports direct .exe files only; received '{extension}'");
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return (false, 0, "The dropped file path is invalid");
        }

        var baselinePids = GetProcessIds(Path.GetFileNameWithoutExtension(filePath));
        try
        {
            _logger.LogInformation("Starting {FilePath} through FlaUI-controlled Explorer", filePath);
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();
            var knownWindowHandles = GetExplorerWindows(desktop)
                .Select(TryGetWindowHandle)
                .Where(handle => handle != 0)
                .ToHashSet();

            var explorerStart = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/n,\"{directory}\"",
                UseShellExecute = true
            };
            Process.Start(explorerStart);

            var explorerWindow = await WaitForExplorerWindowAsync(desktop, knownWindowHandles);
            if (explorerWindow == null)
            {
                return (false, 0, "Timed out waiting for an Explorer window to appear");
            }

            var windowHandle = TryGetWindowHandle(explorerWindow);
            lock (_stateLock)
            {
                _lastExplorerWindowHandle = windowHandle;
            }

            explorerWindow.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_D);
            Keyboard.Type(directory);
            Keyboard.Type(VirtualKeyShort.ENTER);

            var targetItem = await WaitForFileItemAsync(explorerWindow, fileName);
            if (targetItem == null)
            {
                return (false, 0, $"Explorer did not expose '{fileName}' through UI Automation");
            }

            if (targetItem.Patterns.Invoke.IsSupported)
            {
                targetItem.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                targetItem.DoubleClick();
            }

            var pid = await WaitForNewProcessAsync(Path.GetFileNameWithoutExtension(filePath), filePath, baselinePids);
            if (pid == 0)
            {
                if (await HasExecutionBlockDialogAsync(desktop, fileName))
                {
                    return (false, 0, "virus");
                }

                return (false, 0, "Explorer opened the file, but its process could not be identified");
            }

            lock (_stateLock)
            {
                _lastProcessId = pid;
            }

            _ = MonitorProcessAsync(pid);
            _logger.LogInformation("FlaUI started {FilePath} with PID {Pid}", filePath, pid);
            return (true, pid, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlaUI failed to execute {FilePath}", filePath);
            if (ex.Message.Contains("virus", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase))
            {
                return (false, 0, "virus");
            }

            return (false, 0, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync()
    {
        _edrService?.StopCollection();

        int pid;
        nint explorerHandle;
        string droppedFilePath;
        lock (_stateLock)
        {
            pid = _lastProcessId;
            explorerHandle = _lastExplorerWindowHandle;
            droppedFilePath = _droppedFilePath;
        }

        if (pid > 0)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
            catch (ArgumentException)
            {
                // The process already exited.
            }
            catch (InvalidOperationException)
            {
                // The process already exited.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not stop FlaUI execution process {Pid}", pid);
            }
        }

        if (explorerHandle != 0)
        {
            PostMessage(explorerHandle, WmClose, 0, 0);
        }

        if (!string.IsNullOrWhiteSpace(droppedFilePath))
        {
            for (var attempt = 0; attempt < 3 && File.Exists(droppedFilePath); attempt++)
            {
                try
                {
                    File.Delete(droppedFilePath);
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(300);
                }
                catch (UnauthorizedAccessException) when (attempt < 2)
                {
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete dropped file {FilePath}", droppedFilePath);
                    break;
                }
            }
        }

        lock (_stateLock)
        {
            _lastProcessId = 0;
            _lastExplorerWindowHandle = 0;
            _droppedFilePath = string.Empty;
        }

        return (true, "FlaUI execution cleanup completed");
    }

    public Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync()
    {
        lock (_stateLock)
        {
            return Task.FromResult((_lastProcessId, _lastStdout, _lastStderr));
        }
    }

    private async Task MonitorProcessAsync(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            await process.WaitForExitAsync();
            _logger.LogInformation("FlaUI execution process {Pid} exited", pid);
        }
        catch (ArgumentException)
        {
            // The process exited before monitoring began.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error monitoring FlaUI execution process {Pid}", pid);
        }
        finally
        {
            _edrService?.StopCollection();
        }
    }

    private static IReadOnlyCollection<int> GetProcessIds(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Select(process =>
            {
                using (process)
                {
                    return process.Id;
                }
            }).ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private static async Task<int> WaitForNewProcessAsync(
        string processName,
        string expectedPath,
        IReadOnlyCollection<int> baselinePids)
    {
        var deadline = DateTime.UtcNow + ProcessTimeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var candidate in Process.GetProcessesByName(processName))
            {
                using (candidate)
                {
                    if (baselinePids.Contains(candidate.Id))
                    {
                        continue;
                    }

                    try
                    {
                        if (string.Equals(candidate.MainModule?.FileName, expectedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return candidate.Id;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // Access to another process's image path may be restricted.
                    }
                    catch (InvalidOperationException)
                    {
                        // The candidate exited while being inspected.
                    }
                }
            }

            await Task.Delay(PollInterval);
        }

        return 0;
    }

    private static async Task<FlaUI.Core.AutomationElements.AutomationElement?> WaitForExplorerWindowAsync(
        FlaUI.Core.AutomationElements.AutomationElement desktop,
        IReadOnlySet<nint> knownWindowHandles)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var window = GetExplorerWindows(desktop)
                .FirstOrDefault(candidate => !knownWindowHandles.Contains(TryGetWindowHandle(candidate)));
            if (window != null)
            {
                return window;
            }

            await Task.Delay(PollInterval);
        }

        return null;
    }

    private static IEnumerable<FlaUI.Core.AutomationElements.AutomationElement> GetExplorerWindows(
        FlaUI.Core.AutomationElements.AutomationElement desktop)
    {
        return desktop.FindAllChildren(condition => condition.ByClassName("CabinetWClass"));
    }

    private static nint TryGetWindowHandle(FlaUI.Core.AutomationElements.AutomationElement element)
    {
        try
        {
            return (nint)element.Properties.NativeWindowHandle.Value;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<FlaUI.Core.AutomationElements.AutomationElement?> WaitForFileItemAsync(
        FlaUI.Core.AutomationElements.AutomationElement explorerWindow,
        string fileName)
    {
        var deadline = DateTime.UtcNow + UiTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var item = explorerWindow.FindFirstDescendant(condition => condition.ByName(fileName));
            if (item != null && item.ControlType == ControlType.ListItem)
            {
                return item;
            }

            await Task.Delay(PollInterval);
        }

        return null;
    }

    private static async Task<bool> HasExecutionBlockDialogAsync(
        FlaUI.Core.AutomationElements.AutomationElement desktop,
        string fileName)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (DateTime.UtcNow < deadline)
        {
            var dialog = desktop.FindAllChildren(condition => condition.ByClassName("#32770"))
                .FirstOrDefault(element =>
                {
                    try { return element.Name.Contains(fileName, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });

            if (dialog != null)
            {
                var okButton = dialog.FindFirstDescendant(condition => condition.ByControlType(ControlType.Button));
                if (okButton?.Patterns.Invoke.IsSupported == true)
                {
                    okButton.Patterns.Invoke.Pattern.Invoke();
                }
                return true;
            }

            await Task.Delay(PollInterval);
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, uint message, nint wParam, nint lParam);
}