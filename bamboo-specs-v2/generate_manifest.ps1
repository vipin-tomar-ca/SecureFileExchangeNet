# Windows PowerShell version of generate_manifest
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File generate_manifest.ps1 [-OutputPath <path>]
# Auto-detect *AppHost.csproj from solution list in current directory.

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

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# Detect AppHost project from solution in current directory
$projects = & dotnet sln list | ForEach-Object { $_.Trim() }
if ($LASTEXITCODE -ne 0) { throw "Failed to list projects in solution" }
$appHost = $projects | Where-Object { $_ -match 'AppHost\.csproj$' } | Select-Object -First 1
if (-not $appHost) { throw "No AppHost project found in solution" }

Write-Host "Found AppHost project: $appHost"

& dotnet run --project "$appHost" -- generate -p (Join-Path $OutputPath 'aspire-manifest.json')
if ($LASTEXITCODE -ne 0) { throw "Failed to generate Aspire manifest" }

Write-Host "Generated manifest at" (Join-Path $OutputPath 'aspire-manifest.json')
