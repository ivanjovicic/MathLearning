[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $FromMigration,

    [string] $ToMigration,

    [string] $OutputPath,

    [switch] $Idempotent
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repoRoot = Get-MathLearningRepoRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $artifactDirectory = Join-Path $repoRoot 'artifacts\migrations'
    New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null

    $targetMigration = if ([string]::IsNullOrWhiteSpace($ToMigration)) { 'latest' } else { $ToMigration }
    $suffix = if ($Idempotent) { '-idempotent' } else { '' }
    $OutputPath = Join-Path $artifactDirectory ("api-{0}-to-{1}{2}.sql" -f $FromMigration, $targetMigration, $suffix)
}

$arguments = @('ef', 'migrations', 'script', $FromMigration)
if (-not [string]::IsNullOrWhiteSpace($ToMigration)) {
    $arguments += $ToMigration
}

if ($Idempotent) {
    $arguments += '--idempotent'
}

$arguments += @(
    '--project', (Get-InfrastructureProjectPath),
    '--startup-project', (Get-ApiProjectPath),
    '--context', 'ApiDbContext',
    '--output', $OutputPath
)

dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate migration script."
}

Write-Host "Generated migration script: $OutputPath" -ForegroundColor Green