# Requires: PowerShell 5+ and .NET SDK
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File publish_pack.ps1 [-OutputPath <path>]
# When invoked from Bamboo, pass: -OutputPath "$env:bamboo_output_path"

param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Resolve-FirstNonEmpty {
    param([string[]]$Values, [string]$Default)
    foreach ($v in $Values) { if (-not [string]::IsNullOrWhiteSpace($v)) { return $v } }
    return $Default
}

$OutputPath = Resolve-FirstNonEmpty @($OutputPath, $env:bamboo_output_path) 'artifacts'

Write-Host "Using output path: $OutputPath"

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# List projects from current directory solution
$projectsRaw = & dotnet sln list | ForEach-Object { $_.Trim() }
if ($LASTEXITCODE -ne 0) { throw "Failed to list projects in solution" }

$projects = $projectsRaw | Where-Object { $_ -like '*.csproj' }
if (-not $projects) { throw "No .csproj projects found in solution" }

foreach ($project in $projects) {
    $projectPath = $project -replace '^["\s]+|["\s]+$', ''
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $outputDir = Join-Path $OutputPath (Join-Path 'services' (Join-Path $projectName 'publish'))

    Write-Host "Publishing $projectPath to $outputDir"
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    & dotnet publish "$projectPath" -c Release -o "$outputDir"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for '$projectPath'" }

    $manifestPath = Join-Path $OutputPath 'aspire-manifest.json'
    if (Test-Path $manifestPath) {
        Copy-Item -Force -Path $manifestPath -Destination (Join-Path $outputDir 'appsettings.json')
    } else {
        Write-Host "Manifest not found at $manifestPath; skipping copy"
    }

    $zipPath = Join-Path $OutputPath ("$projectName.zip")
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath $zipPath -Force
    Write-Host "Packaged $zipPath"
}

# Clean up temporary publish directories and other artifacts, keep only zip files
Write-Host "Cleaning up temporary files..."
$servicesDir = Join-Path $OutputPath 'services'
if (Test-Path $servicesDir) {
    Remove-Item -Recurse -Force $servicesDir
    Write-Host "Removed temporary services directory"
}

# Remove manifest file if it exists (since it's copied to each package)
$manifestPath = Join-Path $OutputPath 'aspire-manifest.json'
if (Test-Path $manifestPath) {
    Remove-Item -Force $manifestPath
    Write-Host "Removed temporary manifest file"
}

# List remaining zip files
$zipFiles = Get-ChildItem -Path $OutputPath -Filter "*.zip"
Write-Host "Final artifacts:"
foreach ($zip in $zipFiles) {
    Write-Host "  - $($zip.Name)"
}
