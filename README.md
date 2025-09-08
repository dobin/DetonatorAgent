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
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

## Example Usage

### Execute a file
```bash
curl -X POST "https://localhost:5001/api/execute/exec" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@malware.exe" \
  -F "path=C:\RedEdr\data\" \
  -F "fileargs=--verbose"
```

### Get EDR logs
```bash
curl "https://localhost:5001/api/logs/edr"
```

### Check resource lock
```bash
curl "https://localhost:5001/api/lock/status"
```
