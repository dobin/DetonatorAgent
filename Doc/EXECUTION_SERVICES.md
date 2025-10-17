# Execution Services

## Overview

The DetonatorAgent now supports multiple execution service implementations that can be selected at runtime via the `executiontype` parameter in the `/api/execute/exec` endpoint.

## Available Execution Types

### Windows
When running on Windows, the following execution types are available:

- **`exec`** - Uses `WindowsExecutionServiceExec`
  - Simple process execution using `Process.Start()`
  - Supports ISO mounting and ZIP extraction
  - Use `UseShellExecute = true`

- **`autoit`** - Uses `WindowsExecutionServiceAutoit`
  - Uses AutoIt automation library for GUI interaction
  - Supports ISO mounting and ZIP extraction
  - Better for GUI applications that need interaction

- **`autoitexplorer`** - Uses `WindowsExecutionServiceAutoItExplorer`
  - Advanced AutoIt-based execution with Explorer integration
  - Can handle complex GUI scenarios
  - Supports ISO mounting and ZIP extraction

### Linux
When running on Linux, the following execution type is available:

- **`linux`** - Uses `LinuxExecutionService`
  - Simple process execution for Linux
  - Sets executable permissions automatically
  - Does not support archive extraction or ISO mounting

## API Usage

### Get Available Execution Types

**Endpoint:** `GET /api/execute/types`

**Response:**
```json
{
  "types": ["exec", "autoit", "autoitexplorer"],
  "default": "exec"
}
```

This endpoint returns:
- `types`: Array of all available execution type names for the current OS
- `default`: The default execution type that will be used if no `executiontype` parameter is specified

### Execute File with Specific Execution Type

**Endpoint:** `POST /api/execute/exec`

**Parameters:**
- `file` (required) - The file to execute (multipart/form-data)
- `path` (optional) - Target path to write the file
- `fileargs` (optional) - Arguments to pass to the executable
- `executeFile` (optional) - Specific file to execute within an archive
- **`executiontype`** (optional) - The execution service to use (e.g., "exec", "autoit", "autoitexplorer")

**Example using PowerShell:**
```powershell
# Using default execution service
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/execute/exec" `
    -Method POST `
    -Form @{
        file = Get-Item "malware.exe"
    }

# Using specific execution service
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/execute/exec" `
    -Method POST `
    -Form @{
        file = Get-Item "malware.exe"
        executiontype = "autoit"
    }
```

**Example using curl:**
```bash
# Using default execution service
curl -X POST http://localhost:5000/api/execute/exec \
  -F "file=@malware.exe"

# Using specific execution service
curl -X POST http://localhost:5000/api/execute/exec \
  -F "file=@malware.exe" \
  -F "executiontype=exec"
```

**Response:**
```json
{
  "status": "ok",
  "pid": 12345
}
```

Or if invalid execution type is provided:
```json
{
  "status": "error",
  "message": "Invalid execution type 'invalid'. Available types: exec, autoit, autoitexplorer"
}
```

### Kill Last Execution

**Endpoint:** `POST /api/execute/kill`

The kill endpoint automatically uses the last execution service that was used to start a process, so you don't need to specify the execution type again.

## Architecture

### IExecutionServiceProvider

The `IExecutionServiceProvider` interface manages all execution service implementations:

- Registers all available execution services at startup
- Maps friendly names (e.g., "exec", "autoit") to service implementations
- Tracks the last used execution service for operations like kill
- Returns available execution types for the current platform

### Service Registration

All execution services are registered in `Program.cs`:

```csharp
// Register platform-specific services
if (OperatingSystem.IsWindows()) {
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceExec>();
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceAutoit>();
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceAutoItExplorer>();
}
else {
    builder.Services.AddSingleton<IExecutionService, LinuxExecutionService>();
}

// Register the provider
builder.Services.AddSingleton<IExecutionServiceProvider, ExecutionServiceProvider>();
```

### Controller Usage

The `ExecuteController` uses the provider to get the appropriate service:

```csharp
var executionService = _executionServiceProvider.GetExecutionService(executiontype);
if (executionService == null) {
    // Return error with available types
}
```

## Default Behavior

If no `executiontype` parameter is provided, the default service for the platform is used:

- **Windows:** `exec` (WindowsExecutionServiceExec) - Simple and reliable process execution
- **Linux:** `linux` (LinuxExecutionService) - Standard Linux process execution

The default is always the first service registered for each platform, which is intentionally set to the most straightforward implementation.

## Notes

- The execution type is case-insensitive (e.g., "Exec", "EXEC", "exec" all work)
- Only execution types available for the current OS will be registered
- The kill endpoint uses the last execution service that was used, tracked automatically by the provider
