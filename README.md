# DetonatorAgent

A cross-platform ASP.NET Core Web API for malware execution, log collection, and EDR testing.

## Features

- **File execution**: Upload and execute file
- **Multi-platform logging**: System logs, EDR logs, execution logs, and agent logs
- **Process management**: Execute and kill processes with PID tracking
- **Resource locking**: Reserve the agent for a user for some time
- **EDR integration**: Retrieve the AV/EDR logs
- **Swagger documentation**: Built-in API documentation

## API Endpoints

### Execute Controller
- **POST /api/execute/exec** - Upload and execute a file
  - Supports ZIP file extraction to %LOCALAPPDATA%\Temp
  - Parameters:
    - `file`: File to upload (required)
    - `path`: Target directory (optional, defaults to C:\RedEdr\data\)
    - `fileargs`: Command line arguments (optional)
    - `executeFile`: Specific file to execute from ZIP (optional, defaults to alphabetically first .exe/.bat/.com/.lnk)
- **POST /api/execute/kill** - Kill the last executed process

### Logs Controller
- **GET /api/logs/edr** - Get EDR logs with version info
- **GET /api/logs/execution** - Get execution logs (PID, stdout, stderr)
- **GET /api/logs/agent** - Get agent internal logs
- **DELETE /api/logs/agent** - Clear agent logs

### Lock Controller
- **POST /api/lock/acquire** - Acquire resource lock
- **POST /api/lock/release** - Release resource lock  
- **GET /api/lock/status** - Check lock status

## Running the Application

### Prerequisites
- .NET 8.0 SDK

### Start the API
```powershell
dotnet run
```

The API will be available at:
- HTTP: http://localhost:8080
- Swagger UI: https://localhost:8080/swagger


## Usage with script

```
> .\scan-file.ps1 -filepath C:\Tools\procexp64.exe    
=== Simple DetonatorAgent Workflow ===
File: C:\Tools\procexp64.exe
Base URL: http://localhost:8080

Step 1: Acquiring lock...
Lock acquired successfully

Step 2: Executing file...
File executed successfully
Response: {"status":"ok","pid":122980,"message":null}

Step 3: Waiting 10 seconds...
  10 seconds remaining...
  9 seconds remaining...
  8 seconds remaining...
  7 seconds remaining...
  6 seconds remaining...
  5 seconds remaining...
  4 seconds remaining...
  3 seconds remaining...
  2 seconds remaining...
  1 seconds remaining...
Wait completed

Step 4: Retrieving logs...
  Getting EDR logs...
  EDR logs retrieved
  Response: {"logs":"<Events>\r\n</Events>\r\n","edr_version":"Windows Defender 1.0","plugin_version":"1.0"}
  Getting execution logs...
  Execution logs retrieved
  Getting agent logs...
  Agent logs retrieved

Step 5: Killing process...
Process killed successfully
Response: {"status":"ok","message":"Process killed successfully"}

Step 6: Releasing lock...
Lock released successfully

=== Workflow completed ===
```


## Example Usage with curl

### Regular file execution:
```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\psexec64.exe" -F "path=C:\temp\" -F "fileargs=--help"
```

### ZIP file extraction and execution:
```bash
# Extract ZIP and run alphabetically first executable file
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\malware\payload.zip" -F "path=C:\temp\"

# Extract ZIP and run specific file
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\malware\payload.zip" -F "path=C:\temp\" -F "executeFile=malware.exe"
```

### Get the EDR logs:

kill it:
```bash
curl.exe -s -X POST http://localhost:8080/api/execute/kill 
```
