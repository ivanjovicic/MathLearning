param(
    [string] $ConnectionString
)

# Usage:
# 1) Provide connection string as parameter:
#    ./scripts/apply-migrations.ps1 -ConnectionString "Host=...;Username=...;Password=...;Database=...;"
# 2) Or set environment variable ConnectionStrings__Default beforehand and run without parameter

if (-not $ConnectionString) {
    $ConnectionString = $env:ConnectionStrings__Default
}

if (-not $ConnectionString) {
    Write-Error "Connection string not provided. Pass -ConnectionString or set environment variable ConnectionStrings__Default.";
    exit 1
}

Write-Host "Using connection: $ConnectionString"

# Set temporary environment variable for EF design-time factory
$env:ConnectionStrings__Default = $ConnectionString

try {
    Push-Location "src/MathLearning.Infrastructure"
    Write-Host "Applying ApiDbContext migrations..."
    dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api/MathLearning.Api.csproj
    Pop-Location

    Push-Location "src/MathLearning.Admin"
    Write-Host "Applying AdminDbContext migrations..."
    dotnet ef database update --context AdminDbContext
    Pop-Location

    Write-Host "Migrations applied successfully." -ForegroundColor Green
}
catch {
    Write-Error "Migration failed: $_"
    exit 1
}
finally {
    # Unset temporary env var
    Remove-Item Env:\ConnectionStrings__Default -ErrorAction SilentlyContinue
}
