# Test the new agent logs endpoint
$baseUrl = "http://localhost:5000"

Write-Host "Testing Agent Logs API..." -ForegroundColor Green

# Test GET /api/logs/agent
Write-Host "`nTesting GET /api/logs/agent..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/logs/agent" -Method GET -ContentType "application/json"
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10
    Write-Host "`nLogs Count: $($response.Data.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Generate some activity to create more logs by calling other endpoints
Write-Host "`nGenerating some activity to create logs..." -ForegroundColor Yellow

try {
    # Call the lock status endpoint to generate some logs
    $lockResponse = Invoke-RestMethod -Uri "$baseUrl/api/lock/status" -Method GET -ContentType "application/json"
    Write-Host "Lock status called successfully" -ForegroundColor Green
} catch {
    Write-Host "Lock status error: $($_.Exception.Message)" -ForegroundColor Red
}

# Wait a moment for logs to be processed
Start-Sleep -Seconds 1

# Test GET /api/logs/agent again to see new logs
Write-Host "`nTesting GET /api/logs/agent again..." -ForegroundColor Yellow
try {
    $response2 = Invoke-RestMethod -Uri "$baseUrl/api/logs/agent" -Method GET -ContentType "application/json"
    Write-Host "Response:" -ForegroundColor Cyan
    $response2 | ConvertTo-Json -Depth 10
    Write-Host "`nLogs Count: $($response2.Data.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test DELETE /api/logs/agent (clear logs)
Write-Host "`nTesting DELETE /api/logs/agent (clear logs)..." -ForegroundColor Yellow
try {
    $clearResponse = Invoke-RestMethod -Uri "$baseUrl/api/logs/agent" -Method DELETE -ContentType "application/json"
    Write-Host "Clear Response:" -ForegroundColor Cyan
    $clearResponse | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test GET /api/logs/agent after clearing
Write-Host "`nTesting GET /api/logs/agent after clearing..." -ForegroundColor Yellow
try {
    $response3 = Invoke-RestMethod -Uri "$baseUrl/api/logs/agent" -Method GET -ContentType "application/json"
    Write-Host "Response:" -ForegroundColor Cyan
    $response3 | ConvertTo-Json -Depth 10
    Write-Host "`nLogs Count after clear: $($response3.Data.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAgent Logs API testing completed!" -ForegroundColor Green
