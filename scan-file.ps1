# Simple workflow script for DetonatorAgent
# Usage: .\simple-workflow.ps1 -FilePath "C:\path\to\executable.exe"

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:8080"
)

# Validate input file exists
if (-not (Test-Path $FilePath)) {
    Write-Host "Error: File not found: $FilePath" -ForegroundColor Red
    exit 1
}

Write-Host "=== Simple DetonatorAgent Workflow ===" -ForegroundColor Green
Write-Host "File: $FilePath" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Step 1: Acquire Lock
Write-Host "Step 1: Acquiring lock..." -ForegroundColor Cyan
$lockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/acquire"
$lockStatus = $LASTEXITCODE

if ($lockStatus -ne 0) {
    Write-Host "Error: Failed to acquire lock (curl exit code: $lockStatus)" -ForegroundColor Red
    exit 1
}

Write-Host "Lock acquired successfully" -ForegroundColor Green

try {
    # Step 2: Execute file
    Write-Host "`nStep 2: Executing file..." -ForegroundColor Cyan
    $fileName = [System.IO.Path]::GetFileName($FilePath)
    
    $execResponse = curl.exe -s -X POST "$BaseUrl/api/execute/exec" -F "file=@$FilePath" -F "path=C:\RedEdr\data\" -F "fileargs="
    $execStatus = $LASTEXITCODE
    
    if ($execStatus -ne 0) {
        Write-Host "Error: Failed to execute file (curl exit code: $execStatus)" -ForegroundColor Red
        throw "Execution failed"
    }
    
    Write-Host "File executed successfully" -ForegroundColor Green
    Write-Host "Response: $execResponse" -ForegroundColor Gray
    
    # Step 3: Wait 10 seconds
    Write-Host "`nStep 3: Waiting 10 seconds..." -ForegroundColor Cyan
    for ($i = 10; $i -gt 0; $i--) {
        Write-Host "  $i seconds remaining..." -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
    Write-Host "Wait completed" -ForegroundColor Green
    
    # Step 4: Retrieve logs
    Write-Host "`nStep 4: Retrieving logs..." -ForegroundColor Cyan
    
    # Get EDR logs
    Write-Host "  Getting EDR logs..." -ForegroundColor Gray
    $edrLogs = curl.exe -s -X GET "$BaseUrl/api/logs/edr"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  EDR logs retrieved" -ForegroundColor Green
        Write-Host "  Response: $edrLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve EDR logs" -ForegroundColor Yellow
    }
    
    # Get execution logs
    Write-Host "  Getting execution logs..." -ForegroundColor Gray
    $execLogs = curl.exe -s -X GET "$BaseUrl/api/logs/execution"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Execution logs retrieved" -ForegroundColor Green
        #Write-Host "  Response: $execLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve execution logs" -ForegroundColor Yellow
    }
    
    # Get agent logs
    Write-Host "  Getting agent logs..." -ForegroundColor Gray
    $agentLogs = curl.exe -s -X GET "$BaseUrl/api/logs/agent"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Agent logs retrieved" -ForegroundColor Green
        #Write-Host "  Response: $agentLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve agent logs" -ForegroundColor Yellow
    }
    
    # Step 5: Kill process
    Write-Host "`nStep 5: Killing process..." -ForegroundColor Cyan
    $killResponse = curl.exe -s -X POST "$BaseUrl/api/execute/kill"
    $killStatus = $LASTEXITCODE
    
    if ($killStatus -eq 0) {
        Write-Host "Process killed successfully" -ForegroundColor Green
        Write-Host "Response: $killResponse" -ForegroundColor Gray
    } else {
        Write-Host "Warning: Failed to kill process (curl exit code: $killStatus)" -ForegroundColor Yellow
    }
}
finally {
    # Step 6: Release Lock (always execute)
    Write-Host "`nStep 6: Releasing lock..." -ForegroundColor Cyan
    $unlockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/release"
    $unlockStatus = $LASTEXITCODE
    
    if ($unlockStatus -eq 0) {
        Write-Host "Lock released successfully" -ForegroundColor Green
    } else {
        Write-Host "Warning: Failed to release lock (curl exit code: $unlockStatus)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Workflow completed ===" -ForegroundColor Green
