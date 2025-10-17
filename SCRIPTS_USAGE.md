# DetonatorAgent Scripts Usage Guide

## Overview

This document describes how to use the various test scripts included with DetonatorAgent. All scripts now support the full set of execution parameters.

## Main Scripts

### scan-file.ps1 (Windows)

Complete workflow script that executes a file through DetonatorAgent with lock management and log retrieval.

**Location:** Root directory

**Usage:**
```powershell
.\scan-file.ps1 -FilePath "C:\path\to\file.exe" `
    [-Path "C:\target\path\"] `
    [-FileArgs "arguments"] `
    [-ExecuteFile "file.exe"] `
    [-ExecutionType "exec"] `
    [-BaseUrl "http://localhost:8080"]
```

**Parameters:**
- **FilePath** (Required): Path to the file to execute
- **Path** (Optional): Target directory to write the file (default: `C:\RedEdr\data\`)
- **FileArgs** (Optional): Arguments to pass to the executable
- **ExecuteFile** (Optional): Specific file to execute from a ZIP/ISO archive
- **ExecutionType** (Optional): Execution service to use - `exec`, `autoit`, `autoitexplorer` (default: `exec`)
- **BaseUrl** (Optional): DetonatorAgent API URL (default: `http://localhost:8080`)

**Workflow:**
1. Acquires lock
2. Executes the file with specified parameters
3. Waits 10 seconds
4. Retrieves EDR, execution, and agent logs
5. Kills the process
6. Releases lock

**Examples:**
```powershell
# Basic execution with default settings
.\scan-file.ps1 -FilePath "C:\malware\sample.exe"

# With custom path and arguments
.\scan-file.ps1 -FilePath "C:\malware\sample.exe" -Path "C:\temp\" -FileArgs "-silent -install"

# Using AutoIt execution service
.\scan-file.ps1 -FilePath "C:\malware\gui-app.exe" -ExecutionType "autoit"

# Executing specific file from ZIP archive
.\scan-file.ps1 -FilePath "C:\malware\archive.zip" -ExecuteFile "payload.exe" -ExecutionType "exec"
```

---

### test-exec-endpoint.ps1 (Windows)

Simple test script for the `/api/execute/exec` endpoint with all parameters.

**Location:** `Scripts/test-exec-endpoint.ps1`

**Usage:**
```powershell
.\Scripts\test-exec-endpoint.ps1 `
    [-TestFile "C:\path\to\file.exe"] `
    [-Path "C:\target\path\"] `
    [-FileArgs "arguments"] `
    [-ExecuteFile "file.exe"] `
    [-ExecutionType "exec"] `
    [-BaseUrl "http://localhost:8080"]
```

**Parameters:**
All parameters are optional:
- **TestFile**: File to test with (default: `C:\tools\procexp64.exe`)
- **Path**: Target directory (default: `C:\RedEdr\data\`)
- **FileArgs**: Command line arguments
- **ExecuteFile**: Specific file in archive to execute
- **ExecutionType**: Execution service - `exec`, `autoit`, `autoitexplorer` (default: `exec`)
- **BaseUrl**: API URL (default: `http://localhost:8080`)

**Examples:**
```powershell
# Basic test with default file
.\Scripts\test-exec-endpoint.ps1

# Test with specific file
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\notepad.exe"

# Test with arguments
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\app.exe" -FileArgs "-config test.cfg"

# Test with AutoIt execution
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\gui-app.exe" -ExecutionType "autoit"

# Test ZIP extraction
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\malware\sample.zip" -ExecuteFile "malware.exe"
```

---

### test-api.sh (Linux)

Comprehensive test script for Linux systems that tests multiple API endpoints.

**Location:** `Scripts/test-api.sh`

**Usage:**
```bash
./Scripts/test-api.sh [test_file] [path] [fileargs] [executefile] [executiontype]
```

**Positional Parameters:**
All parameters are optional:
1. **test_file**: Path to file to execute (default: `/tmp/test.sh`)
2. **path**: Target directory (default: `/tmp/`)
3. **fileargs**: Command line arguments
4. **executefile**: Specific file in archive to execute
5. **executiontype**: Execution service - `linux` (default: `linux`)

**Examples:**
```bash
# Basic test (creates test script if needed)
./Scripts/test-api.sh

# Test with specific file
./Scripts/test-api.sh /opt/test/sample.sh

# Test with custom path and arguments
./Scripts/test-api.sh /opt/test/sample.sh /tmp/ "-verbose"

# Test with all parameters
./Scripts/test-api.sh /opt/test/app /tmp/ "-arg1 -arg2" "" linux
```

---

## API Parameters Reference

### Execution Parameters

All execution endpoints (`/api/execute/exec`) support these form parameters:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file` | file | Yes | The file to execute (multipart/form-data) |
| `path` | string | No | Target directory to write the file (default: `C:\RedEdr\data\` on Windows, `/tmp/` on Linux) |
| `fileargs` | string | No | Arguments to pass to the executable |
| `executeFile` | string | No | Specific file to execute from a ZIP/ISO archive |
| `executiontype` | string | No | Execution service type (see below) |

### Execution Types

**Windows:**
- `exec` (default) - Simple process execution using Process.Start()
- `autoit` - AutoIt-based execution with GUI interaction support
- `autoitexplorer` - Advanced AutoIt with Explorer integration

**Linux:**
- `linux` (default) - Standard Linux process execution

To see available types for your platform:
```bash
curl http://localhost:8080/api/execute/types
```

Response:
```json
{
  "types": ["exec", "autoit", "autoitexplorer"],
  "default": "exec"
}
```

---

## Common Use Cases

### 1. Simple Executable
```powershell
# Windows
.\scan-file.ps1 -FilePath "C:\malware\sample.exe"

# Linux
./Scripts/test-api.sh /tmp/sample
```

### 2. Executable with Arguments
```powershell
# Windows
.\scan-file.ps1 -FilePath "C:\malware\app.exe" -FileArgs "-silent -config test.cfg"

# Linux
./Scripts/test-api.sh /tmp/app "" "-silent -config test.cfg"
```

### 3. GUI Application (Windows only)
```powershell
# Use AutoIt for better GUI interaction
.\scan-file.ps1 -FilePath "C:\malware\gui-app.exe" -ExecutionType "autoit"
```

### 4. ZIP Archive
```powershell
# Windows - execute specific file from ZIP
.\scan-file.ps1 -FilePath "C:\malware\archive.zip" -ExecuteFile "malware.exe"

# Or let it find the first executable
.\scan-file.ps1 -FilePath "C:\malware\archive.zip"
```

### 5. ISO File (Windows only)
```powershell
# Mount ISO and execute specific file
.\scan-file.ps1 -FilePath "C:\malware\disk.iso" -ExecuteFile "setup.exe"
```

### 6. Custom Target Directory
```powershell
# Windows
.\scan-file.ps1 -FilePath "C:\malware\sample.exe" -Path "C:\custom\path\"

# Linux
./Scripts/test-api.sh /tmp/sample /opt/target/
```

---

## Testing Execution Types

To test all execution types on Windows:

```powershell
# Test with exec (default)
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\notepad.exe" -ExecutionType "exec"

# Test with autoit
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\notepad.exe" -ExecutionType "autoit"

# Test with autoitexplorer
.\Scripts\test-exec-endpoint.ps1 -TestFile "C:\tools\notepad.exe" -ExecutionType "autoitexplorer"
```

---

## Troubleshooting

### Script not found
Make sure you're in the correct directory:
```powershell
# Windows
cd C:\Users\dobin\source\repos\DetonatorAgent

# Linux
cd /path/to/DetonatorAgent
```

### Permission denied (Linux)
Make scripts executable:
```bash
chmod +x Scripts/*.sh
```

### API not responding
Check if DetonatorAgent is running:
```powershell
# Start the agent
dotnet run
```

### File not found error
Ensure the file path is correct and the file exists:
```powershell
# Windows
Test-Path "C:\path\to\file.exe"

# Linux
ls -l /path/to/file
```

---

## See Also

- [EXECUTION_SERVICES.md](EXECUTION_SERVICES.md) - Detailed documentation on execution services
- [README.md](README.md) - General project documentation
