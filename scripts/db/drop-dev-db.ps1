[CmdletBinding()]
param(
    [string] $ConnectionString
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$resolvedConnectionString = Get-DevelopmentConnectionString -OverrideConnectionString $ConnectionString
$parts = Get-ConnectionStringParts -ConnectionString $resolvedConnectionString

if (-not (Test-IsSafeDevHost -HostName $parts.Host)) {
    throw "Refusing to drop database on non-local host '$($parts.Host)'. Allowed hosts: localhost, 127.0.0.1, ::1, postgres, mathlearning-postgres."
}

Write-Host "About to drop and recreate $(Format-DbTarget -Parts $parts)" -ForegroundColor Red
$confirmation = Read-Host "Type '$($parts.Database)' to continue"
if ($confirmation -ne $parts.Database) {
    throw 'Confirmation mismatch. Aborting without changes.'
}

Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'drop', '--force') -ConnectionString $resolvedConnectionString
Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'update') -ConnectionString $resolvedConnectionString

Write-Host 'Development database was dropped and recreated from migrations.' -ForegroundColor Green