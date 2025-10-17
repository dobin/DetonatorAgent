# DetonatorAgent

A cross-platform Web API for MalDev: execution and EDR log collection.


## Purpose

DetonatorAgent fulfills two purposes: 

* File execution
* EDR log collection. 

So you can use it to see the detection of your MalDev software. It is mostly
used to see if initial access chains are undetected, for RedTeam engagements.


### Feature: File Execution

The `/api/execute/exec` API will execute the given file. So the EDR (or AV) can do its thing.
It is intended to simulate a user clicking the malware: It will use the Windows integrated
default app association to "click" the file.

Supported file extension: 
* `.exe`: Direct execution
* `.zip`: Extract and execute
* `.iso`: Extract and execute

There are different execution types: 
* `Exec`: Uses Windows `Process.Start()` with `UseShellExecute = true`
* `AutoIt`:  Uses AutoIt `AutoItX.Run()` in similar style as `Exec`
* **`AutoItExplorer`**: Most realistic! Opens a `explorer.exe` window with AutoIt and "click" the files


### Feature: EDR Log retrieval

The `/api/logs/edr` will return the log files of your EDR product. 
Currently only Microsoft Defender / MDE is supported. 

Example:
```
> curl.exe http://localhost:8080/api/logs/edr                                                        
{"logs":"<Events>\r\n<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Microsoft-Windows-Windows Defender' Guid='{11cd958a-c507-4ef3-b3f2-5fd9dfbd2c78}'/><EventID>1150</EventID><Version>0</Version><Level>4</Level><Task>0</Task><Opcode>0</Opcode><Keywords>0x8000000000000000</Keywords><TimeCreated SystemTime='2025-10-17T10:31:47.0249874Z'/><EventRecordID>11533</EventRecordID><Correlation/><Execution ProcessID='6140' ThreadID='54144'/><Channel>Microsoft-Windows-Windows Defender/Operational</Channel><Computer>unreal</Computer><Security UserID='S-1-5-18'/></System><EventData><Data Name='Product Name'>Microsoft Defender Antivirus</Data><Data Name='Platform version'>4.18.25080.5</Data><Data Name='Unused'></Data><Data Name='Engine version'>1.1.25090.3001</Data><Data Name='Security intelligence version'>1.439.239.0</Data></EventData></Event>\r\n</Events>\r\n","edr_version":"Windows Defender 1.0","plugin_version":"1.0"}
```



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


## Usage: With curl

### Execute regular file

```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.exe"
```

Optional arguments:
* `path`: Where the file will be stored (default is `C:\Users\Public\Downloads`)
* `fileargs`: Parameter to give the exe (e.g. `--help`)


```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.exe" -F "path=C:\temp\" -F "fileargs=--help"
```


### Execute ZIP/ISO file

This will extract the ZIP and run the alphabetically first executable file inside it:

```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.zip"
```

Note that `path` argument Will define where the ZIP file is being written to (not the exe inside it).

If you want to execute a specific file inside the ZIP:
```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.zip" -F "executeFile=procexp64.exe"
```

* `executeFile`: The file inside the archive to execute


### Get the EDR logs


### Cleanup

Cleanup the last execution: 
* Attempt to kill the started process
* Remove the temporary .zip files
* Unmount mounted D: from iso

```bash
curl.exe -s -X POST http://localhost:8080/api/execute/kill 
```


## Usage: Multiplayer all-in-one script

If you share the DetonatorAgent VM with other team members, use the `scan-file.ps1`
script instead. This will: 
* Lock the DetonatorAgent/VM
* Execute the file
* Retrieve EDR logs
* Cleanup
* Unlock the DetonatorAgent/VM

Example:
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

Step 3: Waiting 10 seconds...CV
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

