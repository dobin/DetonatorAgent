$ErrorActionPreference = "Stop"

# Resolve paths relative to this script so it also works when invoked from a
# different working directory.
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "DetonatorAgent.csproj"
$configPath = Join-Path $projectRoot "azure_config.json"

# Load config
$config = Get-Content -Raw -Path $configPath | ConvertFrom-Json

$sourceDir = Join-Path $projectRoot "bin\Release\net8.0-windows7.0"
$destination = "$env:TEMP\detonatoragent.zip"

# Rebuild from a clean output directory so leftovers from earlier builds are
# never packaged.
if (Test-Path $sourceDir) {
  Write-Output "cleanup: $sourceDir"
  Remove-Item $sourceDir -Force -Recurse
}

# Compile the Windows application project (not the whole solution/test project).
Write-Output "build: $projectFile"
dotnet build $projectFile --configuration Release
if ($LASTEXITCODE -ne 0) {
  throw "DetonatorAgent compilation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $sourceDir "DetonatorAgent.exe") -PathType Leaf)) {
  throw "Compilation did not produce DetonatorAgent.exe in: $sourceDir"
}

if (Test-Path $destination) {
  Write-Output "cleanup: $destination"
  Remove-Item $destination -Force
}

# make a zip (exclude logs directory)
Write-Output "zip: $sourceDir to $destination (excluding logs/)"
Get-ChildItem -Path $sourceDir -Exclude "logs" | Compress-Archive -DestinationPath $destination -Force
if (-not (Test-Path $destination -PathType Leaf)) {
  throw "Failed to create archive: $destination"
}

# Upload zip as blob
Write-Output "upload: $destination"
az storage blob upload `
  --account-name $($config.StorageAccount) `
  --container-name $($config.ContainerName) `
  --name $($config.BlobName) `
  --file $destination `
  --sas-token """$($config.SasToken)""" `
  --overwrite
if ($LASTEXITCODE -ne 0) {
  throw "Azure upload failed with exit code $LASTEXITCODE."
}

# delete zip
Write-Output "cleanup: $destination"
Remove-Item $destination -Force
