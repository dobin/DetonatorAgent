# End-to-End Integration Test for DetonatorAgent
# This script tests the /api/execute/exec endpoint with various file types
# and verifies that each execution completes successfully by checking for
# the creation of c:\temp\a file.

param(
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:8080",
    
    [Parameter(Mandatory=$false)]
    [string]$ToolsPath = "$PSScriptRoot\..\tools"
)

# Test configuration
$TestIndicatorFile = "c:\temp\a"
$TestResults = @()

# Color output functions
function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
}

function Write-TestInfo {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Yellow
}

function Write-TestSuccess {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-TestFailure {
    param([string]$Message)
    Write-Host "[FAILURE] $Message" -ForegroundColor Red
}

# Function to check if the agent is running
function Test-AgentRunning {
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl/api/logs/agent" -Method GET -UseBasicParsing -TimeoutSec 5
        return $true
    }
    catch {
        return $false
    }
}

# Function to clean up the indicator file
function Remove-IndicatorFile {
    if (Test-Path $TestIndicatorFile) {
        Remove-Item $TestIndicatorFile -Force
        Write-TestInfo "Cleaned up indicator file: $TestIndicatorFile"
    }
}

# Function to verify the indicator file was created
function Test-IndicatorFile {
    param([int]$WaitSeconds = 5)
    
    # Wait a bit for the file to be created
    Start-Sleep -Seconds $WaitSeconds
    
    if (Test-Path $TestIndicatorFile) {
        Write-TestSuccess "Indicator file found: $TestIndicatorFile"
        return $true
    }
    else {
        Write-TestFailure "Indicator file NOT found: $TestIndicatorFile"
        return $false
    }
}

# Function to execute a file via the API
function Invoke-ExecuteFile {
    param(
        [string]$FilePath,
        [string]$TestName,
        [string]$ExecutionMode = "exec",
        [string]$ExecutableArgs = ""
    )
    
    Write-TestHeader "Testing: $TestName"
    Write-TestInfo "File: $FilePath"
    Write-TestInfo "Execution Mode: $ExecutionMode"
    
    # Check if test file exists
    if (-not (Test-Path $FilePath)) {
        Write-TestFailure "Test file not found: $FilePath"
        return @{
            TestName = $TestName
            Success = $false
            Message = "File not found"
        }
    }
    
    # Clean up any existing indicator file
    Remove-IndicatorFile
    
    try {
        # Execute the file via API
        Write-TestInfo "Sending POST request to $BaseUrl/api/execute/exec"
        
        # Prepare multipart form data
        $boundary = [System.Guid]::NewGuid().ToString()
        $fileBytes = [System.IO.File]::ReadAllBytes($FilePath)
        $fileName = [System.IO.Path]::GetFileName($FilePath)
        
        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
            "Content-Type: application/octet-stream",
            "",
            [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes),
            "--$boundary",
            "Content-Disposition: form-data; name=`"execution_mode`"",
            "",
            $ExecutionMode
        )
        
        if ($ExecutableArgs) {
            $bodyLines += @(
                "--$boundary",
                "Content-Disposition: form-data; name=`"executable_args`"",
                "",
                $ExecutableArgs
            )
        }
        
        $bodyLines += "--$boundary--"
        
        $body = $bodyLines -join "`r`n"
        
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/execute/exec" `
            -Method Post `
            -ContentType "multipart/form-data; boundary=$boundary" `
            -Body ([System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($body))
        
        Write-TestInfo "Response: $($response | ConvertTo-Json -Compress)"
        
        # Response is already an object
        $responseObj = $response
        
        if ($responseObj.status -eq "ok") {
            Write-TestSuccess "Execution started successfully (PID: $($responseObj.pid))"
            
            # Verify the indicator file was created
            $fileCreated = Test-IndicatorFile -WaitSeconds 5
            
            # If execution was successful, call the kill API
            if ($fileCreated) {
                try {
                    Write-TestInfo "Calling /api/execute/kill for PID: $($responseObj.pid)"
                    $killResponse = Invoke-RestMethod -Uri "$BaseUrl/api/execute/kill" `
                        -Method Post `
                        -ContentType "application/json" `
                        -Body (@{ pid = $responseObj.pid } | ConvertTo-Json)
                    
                    if ($killResponse.status -eq "ok") {
                        Write-TestSuccess "Process killed successfully"
                    }
                    else {
                        Write-TestFailure "Kill API returned: $($killResponse.message)"
                    }
                }
                catch {
                    Write-TestFailure "Error calling kill API: $_"
                }
            }
            
            # Clean up the indicator file
            Remove-IndicatorFile
            
            return @{
                TestName = $TestName
                Success = $fileCreated
                Message = if ($fileCreated) { "Execution verified" } else { "Execution started but indicator file not created" }
                PID = $responseObj.pid
            }
        }
        elseif ($responseObj.status -eq "virus") {
            Write-TestFailure "File was blocked as virus"
            if ($responseObj.message) {
                Write-TestInfo "Server message: $($responseObj.message)"
            }
            return @{
                TestName = $TestName
                Success = $false
                Message = "Blocked by EDR" + $(if ($responseObj.message) { ": $($responseObj.message)" } else { "" })
            }
        }
        else {
            $errorMsg = if ($responseObj.message) { $responseObj.message } else { "Unknown error" }
            Write-TestFailure "Execution failed: $errorMsg"
            if ($responseObj.status) {
                Write-TestInfo "Status: $($responseObj.status)"
            }
            return @{
                TestName = $TestName
                Success = $false
                Message = $errorMsg
            }
        }
    }
    catch {
        $errorMessage = $_.Exception.Message
        Write-TestFailure "Error during test: $errorMessage"
        
        # Try to extract the response body for more details
        if ($_.Exception.Response) {
            try {
                $responseStream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($responseStream)
                $responseBody = $reader.ReadToEnd()
                $reader.Close()
                $responseStream.Close()
                
                if ($responseBody) {
                    Write-TestInfo "Server response body: $responseBody"
                    
                    # Try to parse as JSON to get the message
                    try {
                        $errorObj = $responseBody | ConvertFrom-Json
                        if ($errorObj.message) {
                            $errorMessage = $errorObj.message
                            Write-TestInfo "Server error message: $($errorObj.message)"
                        }
                    }
                    catch {
                        # Not JSON, just use the raw body
                    }
                }
            }
            catch {
                # Failed to read response body
            }
        }
        
        return @{
            TestName = $TestName
            Success = $false
            Message = $errorMessage
        }
    }
}

# Main test execution
Write-TestHeader "DetonatorAgent End-to-End Integration Test"
Write-TestInfo "Base URL: $BaseUrl"
Write-TestInfo "Tools Path: $ToolsPath"
Write-TestInfo "Indicator File: $TestIndicatorFile"

# Check if agent is running
Write-TestInfo "Checking if DetonatorAgent is running..."
if (-not (Test-AgentRunning)) {
    Write-TestFailure "DetonatorAgent is not running at $BaseUrl"
    Write-Host "`nPlease start the agent before running this test." -ForegroundColor Yellow
    exit 1
}
Write-TestSuccess "DetonatorAgent is running"

# Resolve tools path
$ToolsPath = Resolve-Path $ToolsPath -ErrorAction SilentlyContinue
if (-not $ToolsPath) {
    Write-TestFailure "Tools path not found"
    exit 1
}

## ExecutionMode: exec

if ( 1 ) {
    # Test 1: Execute testexe.exe
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testexe.exe" `
        -TestName "Execute testexe.exe" `
        -ExecutionMode "exec"

    # Test 2: Execute testexe.zip (contains testexe.exe)
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testexe.zip" `
        -TestName "Execute testexe.zip (archive extraction)" `
        -ExecutionMode "exec"

    # Test 3: Execute testdll.dll
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testdll.dll" `
        -TestName "Execute testdll.dll" `
        -ExecutionMode "exec" `
        -ExecutableArgs "process"

    # Test 4: Execute testdll.zip (contains testdll.dll)
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testdll.zip" `
        -TestName "Execute testdll.zip (archive extraction)" `
        -ExecutionMode "exec" `
        -ExecutableArgs "process"
}


if ( 1 ) {
    # ExecutionMode: autoitexplorer

    # Test 1: Execute testexe.exe
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testexe.exe" `
        -TestName "Execute testexe.exe" `
        -ExecutionMode "autoitexplorer"

    # Test 2: Execute testexe.zip (contains testexe.exe)
    $TestResults += Invoke-ExecuteFile `
        -FilePath "$ToolsPath\testexe.zip" `
        -TestName "Execute testexe.zip (archive extraction)" `
        -ExecutionMode "autoitexplorer"
}

# Print summary
Write-TestHeader "Test Summary"
$successCount = ($TestResults | Where-Object { $_.Success }).Count
$totalCount = $TestResults.Count

Write-Host "`nTotal Tests: $totalCount" -ForegroundColor Cyan
Write-Host "Passed: $successCount" -ForegroundColor Green
Write-Host "Failed: $($totalCount - $successCount)" -ForegroundColor Red

Write-Host "`nDetailed Results:" -ForegroundColor Cyan
foreach ($result in $TestResults) {
    $status = if ($result.Success) { "[PASS]" } else { "[FAIL]" }
    $color = if ($result.Success) { "Green" } else { "Red" }
    Write-Host "$status $($result.TestName) - $($result.Message)" -ForegroundColor $color
}

# Final cleanup
Remove-IndicatorFile

# Exit with appropriate code
if ($successCount -eq $totalCount) {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`nSome tests failed." -ForegroundColor Red
    exit 1
}
