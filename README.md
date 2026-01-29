# DetonatorAgent

A agent for MalDev execution and EDR log & alert collection for RedTeamers. REST API.


## Purpose

DetonatorAgent fulfills two purposes:

* File execution
* EDR log collection

It is mainly used to see if initial access chains are undetected for RedTeam engagements.
So if your malware is detected (and if yes, why), or not.

It is closely related to [RedEdr](https://github.com/dobin/RedEdr), which collects the same
telemetry as an EDR does. And can be used with [Detonator](https://github.com/dobin/Detonator)
to more reliably detonate MalDev, as shown in [detonator.r00ted.ch](https://detonator.r00ted.ch).
A presentation "Detonator - Repeatable Malware Technique Testing" (given at RTS EMEA 25) will
be made publicly available sometimes maybe. 


## Usage 

Use `execfile.ps1`:

```
> powershell.exe -ep bypass .\execfile.ps1 -file mimikatz.exe -executionmode exec

Executing file...
File Execution status: virus

Wait a bit for EDR to process before getting EDR alerts ...

title                      severity category
-----                      -------- --------
HackTool:Win32/Mimikatz!pz High     Tool    
```

## Functionality

DetonatorAgent only does: 
1) Write the exectuable file to disk
2) Executes it
3) Grabs EDR logs (either way if execution successful or not)

DetonatorAgent does not influence (configure, modify, change...) 
the AV, EDR or Windows in any way. 

If the Antivirus component of the EDR detects the file when
dropped on disk (as we see above with mimikatz), 
the file got categorized as virus and removed by the AV. 
No execution can be performed. The EDR logs will still be grabbed. 
To be able to execute a statically detected file, 
either create a whitelisted directory and use `drop_path`, or 
patch mimikatz so static analysis doesnt detect it anymore. 


## Installation

Install:
* .NET 8.0 SDK
* Asp.Net


If you use Defender or MDE:

```powershell
dotnet run -- --edr=defender
```

The API will be available at:
* http://localhost:8080


## Supported Local EDR

For `--edr=`

* defender
* fibratus


## Feature: File Execution

The `/api/execute/exec` API will execute the given file. So the EDR (or AV) can do its thing.


### Execution Mode: Direct

This will write the given file into the selected directory (`drop_path`). 

If it's a .zip, the content of it will be extracted. If it contains more than one file,
the alphabetically first one will used as executable.

Execution is performed with `Process.start()` with `UseShellExecute=true`, which means
that the file has to have a valid Windows execution handler. For .exe files, it is possible
to give arguments. 

The exception is for `.dll`, which is executed with `rundll32.exe`. The file argument
is then used as DLL export which will be called:
```
rundll32.exe <filepath>,<argument>
```


### Execution Mode: AutoIt

It is intended to simulate a user "clicking" the malware: It will use the Windows integrated
default app association to start the file (be it .exe, .lnk, or others). 

The containers `.zip` and `.iso` will be clicked in explorer to be opened. 
The alphabetically first file will be double-clicked.

![AutoItExplorer Demo](Doc/detonatoragent-autoitexplorer-zip.gif)


## Feature: EDR Log retrieval

The `/api/logs/edr` will return the alerts of your EDR product, between
calling `/api/exec/execute` and calling `/api/exec/kill` OR the current time. 

Example:
```
> curl.exe http://localhost:8080/api/logs/edr
{
  "success": true,
  "alerts": [
    {
      "source": "Defender Local",
      "raw": "{\"Product Name\":\"Microsoft Defender Antivirus...}",
      "alertId": "{3F8AE8C6-70BF-4781-BD6C-2E9C0E996F1D}",
      "title": "HackTool:Win32/Mimikatz!pz",
      "severity": "High",
      "category": "Tool",
      "detectionSource": "Real-Time Protection",
      "detectedAt": "2025-12-31T11:24:41.317+01:00",
      "additionalData": {}
    }
  ],
  "isDetected": true
}
```

## Usage: With curl

### Curl Execution
```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.exe"
```

Optional arguments:
* `drop_path`: Where the file will be stored (default is `C:\Users\Public\Downloads`)
* `excecution_mode`: One of the execution modes (`exec`, `autoit`)
* `executable_args`: Parameter to give the exe (e.g. `--help`) (only for `exec` mode)


```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.zip" -F "drop_path=C:\temp\" -F "execution_mode=autoit"
```

```bash
curl.exe -X POST http://localhost:8080/api/execute/exec -F "file=@c:\tools\procexp64.exe" -F "drop_path=C:\temp\" -F "executable_args=--help" -F "execution_mode=exec"
```



### Curl EDR Logs 

Grab the EDR logs:

```bash
curl.exe -s -X POST http://localhost:8080/api/logs/edr
```

It will return all EDR events between:
* Start of execution with `/api/execute/exec`
* Stop of execution with `/api/execute/kill` - OR current time


### Curl Cleanup

Cleanup the last execution: 
* Attempt to kill the started process
* Remove the temporary .zip files
* Unmount mounted D: from iso

```bash
curl.exe -s -X POST http://localhost:8080/api/execute/kill 
```
