[CmdletBinding()]
param(
    [string] $ConnectionString
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$resolvedConnectionString = Get-DevelopmentConnectionString -OverrideConnectionString $ConnectionString
$parts = Get-ConnectionStringParts -ConnectionString $resolvedConnectionString

Write-Host "Applying API migrations to $(Format-DbTarget -Parts $parts)" -ForegroundColor Cyan
Invoke-ApiEfDatabaseCommand -EfArgs @('database', 'update') -ConnectionString $resolvedConnectionString
Write-Host 'API migrations applied successfully.' -ForegroundColor Green