#!/usr/bin/env pwsh
# ============================================
# MathLearning — Local Database Setup
# ============================================

Write-Host "🚀 Starting local PostgreSQL database..." -ForegroundColor Cyan

# Start Docker Compose
docker-compose up -d

# Wait for PostgreSQL to be ready
Write-Host "⏳ Waiting for PostgreSQL to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Check if container is running
$containerStatus = docker ps --filter "name=mathlearning-postgres" --format "{{.Status}}"
if ($containerStatus -like "*Up*") {
    Write-Host "✅ PostgreSQL is running!" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to start PostgreSQL" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📊 Running EF Core migrations..." -ForegroundColor Cyan

# Apply Admin migrations
Write-Host "  → Admin database..." -ForegroundColor Yellow
Set-Location src/MathLearning.Admin
dotnet ef database update --context AdminDbContext
Set-Location ../..

# Apply API migrations
Write-Host "  → API database..." -ForegroundColor Yellow
Set-Location src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api/MathLearning.Api.csproj
Set-Location ../..

Write-Host ""
Write-Host "✅ Local database setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Connection Info:" -ForegroundColor Cyan
Write-Host "  Host: localhost"
Write-Host "  Port: 5433"
Write-Host "  User: postgres"
Write-Host "  Pass: postgres"
Write-Host "  Databases: mathlearning, mathlearning_admin"
Write-Host ""
Write-Host "💡 To connect with pgAdmin or DBeaver, use the info above." -ForegroundColor Yellow
