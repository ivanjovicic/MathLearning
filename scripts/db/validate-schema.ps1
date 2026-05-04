[CmdletBinding()]
param(
    [string] $ConnectionString,

    [string] $DatabaseName,

    [switch] $KeepDatabase
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$baseConnectionString = Get-DevelopmentConnectionString -OverrideConnectionString $ConnectionString
$baseParts = Get-ConnectionStringParts -ConnectionString $baseConnectionString

if (-not (Test-IsSafeDevHost -HostName $baseParts.Host)) {
    throw "Refusing to create or drop validation databases on non-local host '$($baseParts.Host)'."
}

if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
    $DatabaseName = 'mathlearning_schema_validation_' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
}

$validationConnectionString = Get-ConnectionStringWithDatabaseName -ConnectionString $baseConnectionString -DatabaseName $DatabaseName
$validationParts = Get-ConnectionStringParts -ConnectionString $validationConnectionString

Write-Host "Validating schema against $(Format-DbTarget -Parts $validationParts)" -ForegroundColor Cyan

$connectionSnapshot = Set-TemporaryEnvironmentVariable -Name 'ConnectionStrings__Default' -Value $validationConnectionString
$validationConnectionSnapshot = Set-TemporaryEnvironmentVariable -Name 'DATABASE_SCHEMA_VALIDATION_CONNECTION_STRING' -Value $validationConnectionString
$requiredSnapshot = Set-TemporaryEnvironmentVariable -Name 'SCHEMA_VALIDATION_REQUIRED' -Value '1'

try {
    try {
        Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'drop', '--force') -ConnectionString $validationConnectionString
    }
    catch {
        Write-Host 'Validation database did not exist yet. Continuing with a clean create/update.' -ForegroundColor Yellow
    }

    Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'update') -ConnectionString $validationConnectionString

    dotnet test (Join-Path (Get-MathLearningRepoRoot) 'tests\MathLearning.Tests\MathLearning.Tests.csproj') -c Debug --filter 'Category=DatabaseSchema'
    if ($LASTEXITCODE -ne 0) {
        throw 'Database schema validation tests failed.'
    }

    Write-Host 'Database schema validation passed.' -ForegroundColor Green
}
finally {
    Restore-EnvironmentVariable -Snapshot $requiredSnapshot
    Restore-EnvironmentVariable -Snapshot $validationConnectionSnapshot
    Restore-EnvironmentVariable -Snapshot $connectionSnapshot

    if (-not $KeepDatabase) {
        try {
            Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'drop', '--force') -ConnectionString $validationConnectionString
        }
        catch {
            Write-Host 'Cleanup drop failed. Review the validation database manually if needed.' -ForegroundColor Yellow
        }
    }
}