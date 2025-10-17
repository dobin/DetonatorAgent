# WindowsExecutionServiceAutoItExplorer

## Overview
This service implements file execution using explorer.exe with AutoIt UI automation. It provides a more realistic user interaction pattern by simulating actual user clicks and keyboard input through Windows Explorer.

## Key Features

### 1. Direct Executable Execution (.exe, .bat, .com)
- Opens Windows Explorer with the file selected using `/select` parameter
- Waits for the Explorer window to become active
- Sends `{ENTER}` key to execute the selected file
- Captures the PID of the started process using .NET Process class

### 2. ZIP File Handling
- Extracts the ZIP file to a temporary directory (same as AutoIt version)
- Opens Explorer with the extracted executable
- Uses keyboard navigation to select and execute the file
- Simulates double-clicking the file with `{ENTER}` key
- Cleans up extracted directory on kill

### 3. ISO File Handling
- Opens Explorer with the ISO file selected
- Sends `{ENTER}` to mount the ISO (Windows 10+ native ISO mounting)
- Opens D: drive in a new Explorer window (common ISO mount point)
- Navigates to and executes the specified file using keyboard input
- Unmounts ISO on kill using PowerShell Dismount-DiskImage command

## Implementation Details

### AutoIt UI Automation
The service uses these AutoIt functions:
- `AutoItX.Run()` - Launch explorer.exe and other processes
- `AutoItX.WinWait()` - Wait for Explorer windows to appear
- `AutoItX.WinActivate()` - Bring Explorer window to foreground
- `AutoItX.Send()` - Send keyboard input (`{ENTER}` keys and file name typing)
- `AutoItX.ProcessExists()` - Check if a process is running
- `AutoItX.ProcessClose()` - Kill processes

### Window Management
- Uses SW_SHOW (5) to display Explorer windows visibly
- Uses SW_HIDE (0) for background PowerShell commands
- Waits for windows to activate before sending input (500ms-1500ms delays)

### Process ID Tracking
- Uses .NET `System.Diagnostics.Process.GetProcessesByName()` to find started processes
- Selects the most recently started process by StartTime
- Falls back to returning the explorer.exe PID if target process not found
- Stores last PID for later kill operations

### Explorer Navigation
- **For EXE files**: Opens Explorer with file selected, sends ENTER
- **For ZIP files**: Opens Explorer → Opens ZIP → Types first 3 chars of filename → Sends ENTER
- **For ISO files**: Opens Explorer → Mounts ISO → Opens D: drive → Types first 3 chars of filename → Sends ENTER

## Usage Example

```csharp
// In Program.cs, register the service:
builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceAutoItExplorer>();

// The service will automatically:
// 1. Write the file to disk
// 2. Open explorer.exe
// 3. Navigate and execute using keyboard/mouse simulation
// 4. Track the spawned process
// 5. Clean up on kill
```

## Key Differences from WindowsExecutionServiceAutoit

| Feature | AutoIt (Original) | AutoItExplorer (New) |
|---------|------------------|---------------------|
| Execution Method | Direct `AutoItX.Run()` | Explorer.exe + UI automation |
| User Visibility | Hidden (SW_HIDE) | Visible (SW_SHOW) |
| ZIP Handling | Extract + Direct run | Extract + Explorer navigation |
| ISO Handling | Mount + Direct run | Mount + Explorer navigation |
| Interaction Style | Programmatic | User-simulated |

## EDR Behavior Considerations

This implementation may generate different EDR telemetry because:
1. **explorer.exe** is the parent process instead of direct execution
2. UI automation events are visible (window focus changes, keyboard input)
3. File access patterns match user behavior more closely
4. Process tree shows explorer.exe → target.exe instead of DetonatorAgent.exe → target.exe

## Limitations

- Requires visible desktop (won't work in service/background mode)
- Timing-dependent (delays may need adjustment for slower systems)
- Assumes D: drive for ISO mounting (may fail if D: is in use)
- File name typing uses first 3 characters (may select wrong file if multiple files start with same prefix)
- Cannot capture stdout/stderr (inherited limitation from UI-based execution)

## Future Enhancements

- Support for different ISO mount points (E:, F:, etc.)
- More robust file selection in ZIP/ISO (full filename typing or arrow key navigation)
- Window title detection instead of directory-based activation
- Support for nested archives (ZIP within ZIP)
- Configurable timing delays based on system performance
