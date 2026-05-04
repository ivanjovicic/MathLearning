Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-MathLearningRepoRoot {
    return Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

function Get-ApiProjectPath {
    return Join-Path (Get-MathLearningRepoRoot) 'src\MathLearning.Api\MathLearning.Api.csproj'
}

function Get-InfrastructureProjectPath {
    return Join-Path (Get-MathLearningRepoRoot) 'src\MathLearning.Infrastructure\MathLearning.Infrastructure.csproj'
}

function Get-DevelopmentConnectionString {
    param(
        [string] $OverrideConnectionString
    )

    if (-not [string]::IsNullOrWhiteSpace($OverrideConnectionString)) {
        return $OverrideConnectionString
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__Default)) {
        return $env:ConnectionStrings__Default
    }

    $appSettingsPath = Join-Path (Join-Path (Get-MathLearningRepoRoot) 'src\MathLearning.Api') 'appsettings.Development.json'
    if (-not (Test-Path $appSettingsPath)) {
        throw "Could not find development appsettings file at '$appSettingsPath'."
    }

    $appSettings = Get-Content -Raw -Path $appSettingsPath | ConvertFrom-Json
    $connectionString = [string] $appSettings.ConnectionStrings.Default
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "ConnectionStrings:Default is missing from '$appSettingsPath'."
    }

    return $connectionString
}

function Get-ConnectionStringParts {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ConnectionString
    )

    $parsed = ConvertFrom-ConnectionString -ConnectionString $ConnectionString

    return [ordered]@{
        Host = Get-ConnectionValue -ParsedConnectionString $parsed -Keys @('Host', 'Server', 'Data Source')
        Port = Get-ConnectionValue -ParsedConnectionString $parsed -Keys @('Port')
        Database = Get-ConnectionValue -ParsedConnectionString $parsed -Keys @('Database', 'Initial Catalog')
        Username = Get-ConnectionValue -ParsedConnectionString $parsed -Keys @('Username', 'User ID', 'User Id', 'UID')
    }
}

function Get-ConnectionValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary] $ParsedConnectionString,

        [Parameter(Mandatory = $true)]
        [string[]] $Keys
    )

    foreach ($key in $Keys) {
        if ($ParsedConnectionString.Contains($key)) {
            return [string] $ParsedConnectionString[$key]
        }
    }

    return ''
}

function Format-DbTarget {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary] $Parts
    )

    return "Host=$($Parts.Host);Port=$($Parts.Port);Database=$($Parts.Database);User=$($Parts.Username)"
}

function Test-IsSafeDevHost {
    param(
        [Parameter(Mandatory = $true)]
        [string] $HostName
    )

    if ([string]::IsNullOrWhiteSpace($HostName)) {
        return $false
    }

    $normalized = $HostName.Trim().ToLowerInvariant()
    return $normalized -in @('localhost', '127.0.0.1', '::1', 'postgres', 'mathlearning-postgres')
}

function Set-TemporaryEnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [AllowNull()]
        [string] $Value
    )

    $previousExists = Test-Path "Env:$Name"
    $previousValue = if ($previousExists) { (Get-Item "Env:$Name").Value } else { $null }

    if ($null -eq $Value) {
        Remove-Item "Env:$Name" -ErrorAction SilentlyContinue
    }
    else {
        Set-Item "Env:$Name" $Value
    }

    return [pscustomobject]@{
        Name = $Name
        Exists = $previousExists
        Value = $previousValue
    }
}

function Restore-EnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject] $Snapshot
    )

    if ($Snapshot.Exists) {
        Set-Item "Env:$($Snapshot.Name)" $Snapshot.Value
    }
    else {
        Remove-Item "Env:$($Snapshot.Name)" -ErrorAction SilentlyContinue
    }
}

function Invoke-ApiEfDatabaseCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $EfArgs,

        [Parameter(Mandatory = $true)]
        [string] $ConnectionString
    )

    $snapshot = Set-TemporaryEnvironmentVariable -Name 'ConnectionStrings__Default' -Value $ConnectionString
    try {
        $arguments = @(
            'ef'
        ) + $EfArgs + @(
            '--project', (Get-InfrastructureProjectPath),
            '--startup-project', (Get-ApiProjectPath),
            '--context', 'ApiDbContext'
        )

        dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Restore-EnvironmentVariable -Snapshot $snapshot
    }
}

function Get-ConnectionStringWithDatabaseName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ConnectionString,

        [Parameter(Mandatory = $true)]
        [string] $DatabaseName
    )

    $parsed = ConvertFrom-ConnectionString -ConnectionString $ConnectionString
    if ($parsed.Contains('Database')) {
        $parsed['Database'] = $DatabaseName
    }
    elseif ($parsed.Contains('Initial Catalog')) {
        $parsed['Initial Catalog'] = $DatabaseName
    }
    else {
        $parsed['Database'] = $DatabaseName
    }

    return ConvertTo-ConnectionString -ParsedConnectionString $parsed
}

function ConvertFrom-ConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ConnectionString
    )

    $parts = [ordered]@{}
    foreach ($segment in $ConnectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $pair = $segment.Split('=', 2)
        if ($pair.Length -eq 2) {
            $parts[$pair[0].Trim()] = $pair[1].Trim()
        }
    }

    return $parts
}

function ConvertTo-ConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary] $ParsedConnectionString
    )

    return (($ParsedConnectionString.GetEnumerator() | ForEach-Object {
                "$($_.Key)=$($_.Value)"
            }) -join ';') + ';'
}