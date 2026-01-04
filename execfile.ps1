# Simple workflow script for DetonatorAgent
# Usage: .\scan-file.ps1 -File "C:\path\to\executable.exe" [-DropPath "C:\target\path\"] [-ExecutableArgs "arg1 arg2"] [-ExecutableName "file.exe"] [-ExecutionMode "exec"] [-Runtime 10]
# 
# Parameters:
#   -File: Path to the file to execute (required)
#   -DropPath: Target directory to write the file (optional, default: C:\RedEdr\data\)
#   -ExecutableArgs: Arguments to pass to the executable (optional)
#   -ExecutableName: Specific file to execute from an archive (optional, for ZIP/ISO files)
#   -ExecutionMode: Execution service to use: "exec", "autoit"
#   -Runtime: Duration in seconds to wait before killing the process (optional, default: 10)
#   -BaseUrl: Base URL of the DetonatorAgent API (optional, default: http://localhost:8080)

param(
    [Parameter(Mandatory=$true, HelpMessage="Path to the file to execute")]
    [string]$File,
    
    [Parameter(Mandatory=$false, HelpMessage="Target directory to write the file")]
    [string]$DropPath = "C:\RedEdr\data\",
    
    [Parameter(Mandatory=$false, HelpMessage="Arguments to pass to the executable")]
    [string]$ExecutableArgs = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Specific file to execute from an archive")]
    [string]$ExecutableName = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Execution service type (exec, autoit)")]
    [ValidateSet("exec", "autoit", "clickfix")]
    [string]$ExecutionMode = "autoit",
    
    [Parameter(Mandatory=$false, HelpMessage="Duration in seconds to wait before killing the process")]
    [int]$Runtime = 10,
    
    [Parameter(Mandatory=$false, HelpMessage="Base URL of the DetonatorAgent API")]
    [string]$BaseUrl = "http://localhost:8080"
)

# Validate input file exists
if (-not (Test-Path $File)) {
    Write-Host "Error: File not found: $File" -ForegroundColor Red
    exit 1
}

# Function to get EDR alerts and display only new ones
function Get-EdrAlerts {
    param(
        [Parameter(Mandatory=$true)]
        [string]$BaseUrl,
        
        [Parameter(Mandatory=$false)]
        [int]$Sleep = 1,
        
        [Parameter(Mandatory=$false)]
        [int]$Count = 1
    )

    Write-Host "Poll for alerts every second for $Count seconds..."
    Write-Host ""
    
    # Track seen alerts across all iterations
    $SeenAlerts = @{}
    $HeaderDisplayed = $false
    
    for ($iteration = 0; $iteration -lt $Count; $iteration++) {
        $edrLogs = curl.exe -s -X GET "$BaseUrl/api/logs/edr"
        if ($LASTEXITCODE -eq 0) {
            try {
                $edrResponse = $edrLogs | ConvertFrom-Json
                if ($edrResponse.alerts -and $edrResponse.alerts.Count -gt 0) {
                    # Filter for new alerts only
                    foreach ($alert in $edrResponse.alerts) {
                        if (-not $SeenAlerts.ContainsKey($alert.alertId)) {
                            $SeenAlerts[$alert.alertId] = $true
                            
                            # Display header only once
                            if (-not $HeaderDisplayed) {
                                Write-Host "title                   severity category alertId"
                                Write-Host "-----                   -------- -------- -------"
                                $HeaderDisplayed = $true
                            }
                            
                            # Display new alert as a single row
                            $titlePadded = $alert.title.PadRight(23)
                            $severityPadded = $alert.severity.PadRight(8)
                            $categoryPadded = $alert.category.PadRight(8)
                            Write-Host "$titlePadded $severityPadded $categoryPadded $($alert.alertId)"
                        }
                    }
                } else {
                    #Write-Host "No alerts found" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "  Warning: Failed to parse EDR logs" -ForegroundColor Yellow
                Write-Host "  Response: $edrLogs" -ForegroundColor Gray
            }
        } else {
            Write-Host "  Warning: Failed to retrieve EDR logs" -ForegroundColor Yellow
        }
        
        # Sleep between iterations (but not after the last one)
        if ($Sleep -gt 0 -and $iteration -lt ($Count - 1)) {
            Start-Sleep -Seconds $Sleep
        }
    }
}

Write-Host "File: $File"
Write-Host "Drop Path: $DropPath"
Write-Host "Executable Args: $ExecutableArgs"
Write-Host "Executable Name: $ExecutableName"
Write-Host "Execution Mode: $ExecutionMode"
Write-Host "Runtime: $Runtime seconds"
Write-Host "Base URL: $BaseUrl"
Write-Host ""

# Acquire Lock
#Write-Host "Acquiring lock..." -ForegroundColor Cyan
$lockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/acquire"
$lockStatus = $LASTEXITCODE
if ($lockStatus -ne 0) {
    Write-Host "Error: Failed to acquire lock (curl exit code: $lockStatus)" -ForegroundColor Red
    exit 1
}
#Write-Host "Lock acquired successfully" -ForegroundColor Green

try {
    # Encrypt file
    $xorKey = Get-Random -Minimum 1 -Maximum 256
    $fileBytes = [System.IO.File]::ReadAllBytes($File)
    $encryptedBytes = New-Object byte[] $fileBytes.Length
    for ($i = 0; $i -lt $fileBytes.Length; $i++) {
        $encryptedBytes[$i] = $fileBytes[$i] -bxor $xorKey
    }
    
    # Write encrypted file to temporary location
    $tempFile = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllBytes($tempFile, $encryptedBytes)
    Write-Host "Encrypted file created: $tempFile" -ForegroundColor Gray
    
    # Execute file
    $fileName = [System.IO.Path]::GetFileName($File)
    $curlArgs = @(
        "-s",
        "-X", "POST",
        "$BaseUrl/api/execute/exec",
        "-F", "file=@$tempFile;filename=$fileName",
        "-F", "drop_path=$DropPath",
        "-F", "xor_key=$xorKey"
    )
    # Add optional executable_args parameter
    if ($ExecutableArgs) {
        $curlArgs += "-F"
        $curlArgs += "executable_args=$ExecutableArgs"
    }
    # Add optional executable_name parameter
    if ($ExecutableName) {
        $curlArgs += "-F"
        $curlArgs += "executable_name=$ExecutableName"
    }
    # Add optional execution_mode parameter
    if ($ExecutionMode) {
        $curlArgs += "-F"
        $curlArgs += "execution_mode=$ExecutionMode"
    }
    
    # Execute
    Write-Host "Executing file on DetonatorAgent..."
    $execResponse = & curl.exe $curlArgs
    $execStatus = $LASTEXITCODE
    if ($execStatus -ne 0) {
        Write-Host "Error: Failed to execute file (curl exit code: $execStatus)" -ForegroundColor Red
        throw "Execution failed"
    }
    # Parse and check the execution response
    $status = ""
    try {
        $responseObj = $execResponse | ConvertFrom-Json
        $status = $responseObj.status # "virus", "ok", "error"
    }
    catch {
        Write-Host "Warning: Failed to parse execution response" -ForegroundColor Red
        Write-Host "Response: $execResponse" -ForegroundColor Gray
    }
    Write-Host "File Execution status: $status" -ForegroundColor Yellow

    # Cleanup temporary encrypted file
    if (Test-Path $tempFile) {
        Remove-Item $tempFile -Force
    #    Write-Host "Cleaned up temporary encrypted file" -ForegroundColor Gray
    }

    # Wait & Poll (if execution was successful)
    if ($status -eq "ok") {
        Write-Host "Execution running, waiting $Runtime seconds..."
        Get-EdrAlerts -BaseUrl $BaseUrl -Sleep 1 -Count $Runtime
        Write-Host "Polling/Runtime finished"
    }
    # If its detected on file write, 
    # poll for 3 seconds to allow EDR to process
    if ($status -eq "virus") {
        Get-EdrAlerts -BaseUrl $BaseUrl -Sleep 1 -Count 3
    }

    # Kill process if it ran
    if ($status -eq "ok") {
        Write-Host "Killing process..."
        $killResponse = curl.exe -s -X POST "$BaseUrl/api/execute/kill"
        $killStatus = $LASTEXITCODE
        if ($killStatus -ne 0) {
            Write-Host "Warning: Failed to kill process (curl exit code: $killStatus)" -ForegroundColor Yellow
        }
    }

    # Get execution logs if executed
    #if ($status -eq "ok") {
    #    #Write-Host "  Getting execution logs..." -ForegroundColor Gray
    #    $execLogs = curl.exe -s -X GET "$BaseUrl/api/logs/execution"
    #    if ($LASTEXITCODE -eq 0) {
    #        #Write-Host "  Execution logs retrieved" -ForegroundColor Green
    #        #Write-Host "  Response: $execLogs" -ForegroundColor Gray
    #    } else {
    #        Write-Host "  Warning: Failed to retrieve execution logs" -ForegroundColor Yellow
    #    }
    #}
    
    # Get agent logs
    #Write-Host "  Getting agent logs..." -ForegroundColor Gray
    #$agentLogs = curl.exe -s -X GET "$BaseUrl/api/logs/agent"
    #if ($LASTEXITCODE -ne 0) {
    #    Write-Host "  Warning: Failed to retrieve agent logs" -ForegroundColor Yellow
    #}

}
finally {
    # Release Lock (always execute)
    #Write-Host "`nReleasing lock..." -ForegroundColor Cyan
    $unlockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/release"
    $unlockStatus = $LASTEXITCODE
    if ($unlockStatus -ne 0) {
        Write-Host "Warning: Failed to release lock (curl exit code: $unlockStatus)" -ForegroundColor Yellow
    }
}
