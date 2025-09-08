# Test the DetonatorAgent API

# Start the API (run this in a separate terminal)
# dotnet run

# Wait for the API to start, then test the endpoints

Write-Host "Testing DetonatorAgent API..." -ForegroundColor Green

# Test /api/logs endpoint
Write-Host "`nTesting GET /api/logs..." -ForegroundColor Yellow
try {
    $logsResponse = Invoke-RestMethod -Uri "http://localhost:8080/api/logs" -Method GET
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($logsResponse | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test /api/execute endpoint
Write-Host "`nTesting POST /api/execute..." -ForegroundColor Yellow
try {
    $executeBody = @{
        command = "echo Hello World"
    } | ConvertTo-Json

    $executeResponse = Invoke-RestMethod -Uri "http://localhost:8080/api/execute" -Method POST -Body $executeBody -ContentType "application/json"
    Write-Host "Success!" -ForegroundColor Green
    Write-Host "Response: $($executeResponse | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAPI testing complete!" -ForegroundColor Green
Write-Host "Visit http://localhost:8080/swagger for the Swagger UI" -ForegroundColor Cyan
