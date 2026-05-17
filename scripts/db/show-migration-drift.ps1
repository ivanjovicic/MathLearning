[CmdletBinding()]
param(
    [string] $ConnectionString
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$resolvedConnectionString = Get-DevelopmentConnectionString -OverrideConnectionString $ConnectionString
$parts = Get-ConnectionStringParts -ConnectionString $resolvedConnectionString

Write-Host "Inspecting migration drift for $(Format-DbTarget -Parts $parts)" -ForegroundColor Cyan
Write-Host ''
Write-Host 'Migrations in code (EF migration set):' -ForegroundColor Yellow

$connectionSnapshot = Set-TemporaryEnvironmentVariable -Name 'ConnectionStrings__Default' -Value $resolvedConnectionString
try {
    $efArgs = @(
        'ef',
        'migrations',
        'list',
        '--project', (Get-InfrastructureProjectPath),
        '--startup-project', (Get-ApiProjectPath),
        '--context', 'ApiDbContext'
    )

    dotnet @efArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($efArgs -join ' ') failed with exit code $LASTEXITCODE."
    }
}
finally {
    Restore-EnvironmentVariable -Snapshot $connectionSnapshot
}

Write-Host ''
Write-Host 'Migrations applied in database (__EFMigrationsHistory):' -ForegroundColor Yellow

$psqlCommand = Get-Command 'psql' -ErrorAction SilentlyContinue
if ($null -eq $psqlCommand) {
    Write-Host 'psql was not found in PATH, so applied migrations could not be queried automatically.' -ForegroundColor Yellow
    Write-Host 'Run this manually if needed: SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' -ForegroundColor Yellow
}
else {
    $parsedConnectionString = ConvertFrom-ConnectionString -ConnectionString $resolvedConnectionString
    $username = Get-ConnectionValue -ParsedConnectionString $parsedConnectionString -Keys @('Username', 'User ID', 'User Id', 'UID')
    $password = Get-ConnectionValue -ParsedConnectionString $parsedConnectionString -Keys @('Password', 'Pwd')
    $port = if ([string]::IsNullOrWhiteSpace($parts.Port)) { '5432' } else { $parts.Port }

    if ([string]::IsNullOrWhiteSpace($parts.Host) -or
        [string]::IsNullOrWhiteSpace($parts.Database) -or
        [string]::IsNullOrWhiteSpace($username)) {
        Write-Host 'Connection string is missing Host, Database, or Username; unable to query applied migrations automatically.' -ForegroundColor Yellow
        Write-Host 'Run this manually if needed: SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' -ForegroundColor Yellow
    }
    else {
        $passwordSnapshot = $null
        if (-not [string]::IsNullOrWhiteSpace($password)) {
            $passwordSnapshot = Set-TemporaryEnvironmentVariable -Name 'PGPASSWORD' -Value $password
        }

        try {
            & $psqlCommand.Source `
                -h $parts.Host `
                -p $port `
                -U $username `
                -d $parts.Database `
                -At `
                -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'

            if ($LASTEXITCODE -ne 0) {
                Write-Host 'Unable to query __EFMigrationsHistory automatically. Verify credentials and connectivity, then retry.' -ForegroundColor Yellow
            }
        }
        finally {
            if ($null -ne $passwordSnapshot) {
                Restore-EnvironmentVariable -Snapshot $passwordSnapshot
            }
        }
    }
}

Write-Host ''
Write-Host 'Unknown applied migrations are migrations present in the database but missing from the current code. For local dev, run scripts/db/drop-dev-db.ps1 if data is disposable.' -ForegroundColor Cyan
