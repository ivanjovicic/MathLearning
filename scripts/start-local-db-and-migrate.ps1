<#
Usage:
  ./scripts/start-local-db-and-migrate.ps1

This script:
  - Starts the local postgres via docker-compose (docker must be running)
  - Waits until Postgres reports healthy (pg_isready)
  - Runs the apply-migrations.ps1 script with local connection string
#>

param(
    [string] $DbHost = "localhost",
    [int] $DbPort = 5432,
    [string] $DbName = "neondb",
    [string] $DbUser = "neondb_owner",
    [string] $DbPassword = "npg_WB4rnl2CQamX",
    [int] $MaxWaitSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Starting local Postgres using docker-compose..."

# Start docker compose
docker compose up -d --build
if ($LASTEXITCODE -ne 0) {
    Write-Error "docker compose up failed"
    exit 1
}

# Get container id for service 'postgres'
$containerId = docker compose ps -q postgres
if ([string]::IsNullOrWhiteSpace($containerId)) {
    Write-Host "Could not find postgres container from docker compose. Listing containers..."
    docker ps --filter "ancestor=postgres" --format "{{.ID}} {{.Names}}"
}

Write-Host "Waiting for Postgres to become ready (max $MaxWaitSeconds seconds)..."

$start = Get-Date
while ($true) {
    try {
        docker exec $containerId pg_isready -U postgres > $null 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Postgres is ready."
            break
        }
    }
    catch {
        # ignore
    }

    if ((Get-Date) - $start -gt (New-TimeSpan -Seconds $MaxWaitSeconds)) {
        Write-Error "Timed out waiting for Postgres to be ready"
        docker compose logs postgres --tail 100
        exit 1
    }

    Start-Sleep -Seconds 2
}

# Build connection string for local DB
$connectionString = "Host=$DbHost;Port=$DbPort;Username=$DbUser;Password=$DbPassword;Database=$DbName;"
Write-Host "Applying EF migrations using connection: $connectionString"

# Call apply-migrations.ps1 which applies ApiDbContext and AdminDbContext migrations
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$applyScript = Join-Path -Path $scriptDir -ChildPath "apply-migrations.ps1"
if (-not (Test-Path $applyScript)) {
    Write-Error "apply-migrations.ps1 not found in scripts/"
    exit 1
}

# Use Windows PowerShell if pwsh not available
$pwshExists = (Get-Command pwsh -ErrorAction SilentlyContinue) -ne $null
if ($pwshExists) {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $applyScript -ConnectionString $connectionString
} else {
    Powershell -NoProfile -ExecutionPolicy Bypass -File $applyScript -ConnectionString $connectionString
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "apply-migrations failed"
    exit 1
}

Write-Host "Migrations applied successfully. Local Postgres + DB ready." -ForegroundColor Green
