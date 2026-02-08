<#
Usage:
  ./scripts/start-local-db-and-migrate.ps1

This script:
  - Starts the local postgres via docker-compose (docker must be running)
  - Waits until Postgres reports healthy (pg_isready)
  - Runs EF Core migrations against the local database
#>

param(
    [string] $DbHost = "localhost",
    [int] $DbPort = 5433,
    [string] $DbName = "mathlearning",
    [string] $DbUser = "postgres",
    [string] $DbPassword = "postgres",
    [int] $MaxWaitSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "=========================================="
Write-Host " MathLearning Local Dev Setup"
Write-Host "=========================================="
Write-Host ""

# ── 1) Start docker compose ──────────────────────────
Write-Host "[1/4] Starting PostgreSQL container..." -ForegroundColor Yellow
docker compose up -d --build
if ($LASTEXITCODE -ne 0) {
    Write-Error "docker compose up failed. Is Docker Desktop running?"
    exit 1
}

# ── 2) Wait for Postgres ─────────────────────────────
Write-Host "[2/4] Waiting for PostgreSQL to become ready (max $MaxWaitSeconds seconds)..." -ForegroundColor Yellow

$containerId = docker compose ps -q postgres
if ([string]::IsNullOrWhiteSpace($containerId)) {
    Write-Host "Could not find postgres container. Listing all containers..." -ForegroundColor Red
    docker ps --format "{{.ID}} {{.Names}} {{.Image}}"
    exit 1
}

$start = Get-Date
while ($true) {
    try {
        docker exec $containerId pg_isready -U postgres 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "PostgreSQL is ready!" -ForegroundColor Green
            break
        }
    }
    catch { }

    if ((Get-Date) - $start -gt (New-TimeSpan -Seconds $MaxWaitSeconds)) {
        Write-Error "Timed out waiting for PostgreSQL"
        docker compose logs postgres --tail 50
        exit 1
    }

    Start-Sleep -Seconds 2
}

# ── 3) Run EF Core migrations ────────────────────────
Write-Host "[3/4] Running EF Core migrations..." -ForegroundColor Yellow

$connectionString = "Host=$DbHost;Port=$DbPort;Username=$DbUser;Password=$DbPassword;Database=$DbName;"

$env:ConnectionStrings__Default = $connectionString

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)

dotnet ef database update `
    --project "$repoRoot\src\MathLearning.Infrastructure\MathLearning.Infrastructure.csproj" `
    --startup-project "$repoRoot\src\MathLearning.Api\MathLearning.Api.csproj" `
    --context ApiDbContext

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "EF migration failed!" -ForegroundColor Red
    Write-Host "Make sure dotnet-ef tool is installed:" -ForegroundColor Red
    Write-Host "  dotnet tool install --global dotnet-ef" -ForegroundColor Yellow
    exit 1
}

# ── 4) Done ───────────────────────────────────────────
Write-Host ""
Write-Host "[4/4] Done!" -ForegroundColor Green
Write-Host ""
Write-Host "=========================================="
Write-Host " Local environment is ready!"
Write-Host "=========================================="
Write-Host ""
Write-Host "  PostgreSQL: localhost:5432" -ForegroundColor White
Write-Host "  Database:   $DbName" -ForegroundColor White
Write-Host "  User:       $DbUser" -ForegroundColor White
Write-Host "  Password:   $DbPassword" -ForegroundColor White
Write-Host ""
Write-Host "  Run the API:" -ForegroundColor White
Write-Host "    dotnet run --project src\MathLearning.Api" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Run the Admin:" -ForegroundColor White
Write-Host "    dotnet run --project src\MathLearning.Admin" -ForegroundColor Yellow
Write-Host ""
