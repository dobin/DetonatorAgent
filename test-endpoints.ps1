# Test script for the /api/execute/exec and /api/execute/kill endpoints

Write-Host "Testing DetonatorAgent endpoints..."

# Create a simple test executable (a batch file for Windows)
$testFile = "C:\temp\test.bat"
$testContent = "@echo off`necho Hello from test executable`ntimeout /t 10 /nobreak > nul`necho Test completed"

# Ensure temp directory exists
if (!(Test-Path "C:\temp")) {
    New-Item -ItemType Directory -Path "C:\temp" -Force
}

# Create the test file
Set-Content -Path $testFile -Value $testContent
Write-Host "Created test file: $testFile"

Write-Host "`n1. Testing /api/execute/exec endpoint..."

# Test the endpoint using curl
try {
    $response = curl.exe -s -X POST http://localhost:5000/api/execute/exec `
        -F "file=@$testFile" `
        -F "path=C:\temp\" `
        -F "fileargs="
    
    Write-Host "Response from /api/execute/exec:"
    Write-Host $response
    
    # Parse JSON to get PID (basic parsing)
    if ($response -match '"pid":(\d+)') {
        $pid = $matches[1]
        Write-Host "Started process with PID: $pid"
    }
    
    Start-Sleep -Seconds 2
    
    Write-Host "`n2. Testing /api/execute/kill endpoint..."
    
    $killResponse = curl.exe -s -X POST http://localhost:5000/api/execute/kill
    Write-Host "Response from /api/execute/kill:"
    Write-Host $killResponse
    
} catch {
    Write-Host "Error occurred: $_"
}

Write-Host "`nTest completed."
