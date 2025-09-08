# Test script for the new /api/execute/exec endpoint

$testFile = "C:\tools\procexp64.exe"
Write-Host "Testing /api/execute/exec endpoint..."

# Test the endpoint using curl
$curlCommand = @"
curl.exe -X POST http://localhost:5000/api/execute/exec -F "file=@$testFile" -F "path=C:\temp\" -F "fileargs="
"@

Write-Host "Running: $curlCommand"
Invoke-Expression $curlCommand

Write-Host "`nTest completed."
