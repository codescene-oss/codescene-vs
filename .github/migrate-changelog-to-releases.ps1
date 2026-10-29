Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'release-flow.psm1') -Force

$script:DefaultRepository = 'codescene-oss/codescene-vs'
$script:ChangelogFileName = 'CHANGELOG.md'
$script:Usage = 'Usage: migrate-changelog-to-releases.ps1 [--apply] [--file <path>]'

function Get-GitHubRepository {
    $repository = [Environment]::GetEnvironmentVariable('GITHUB_REPOSITORY')
    if ([string]::IsNullOrWhiteSpace($repository)) { return $script:DefaultRepository }
    return $repository
}

function Read-MigrationArguments($Arguments) {
    $options = [pscustomobject]@{ Apply = $false; File = '' }
    $index = 0
    while ($index -lt $Arguments.Count) {
        $argument = [string]$Arguments[$index]
        switch -CaseSensitive ($argument) {
            '--apply' { $options.Apply = $true }
            '--file' {
                $index++
                if ($index -ge $Arguments.Count -or [string]::IsNullOrWhiteSpace([string]$Arguments[$index])) {
                    throw "--file requires a path. $($script:Usage)"
                }
                $options.File = [string]$Arguments[$index]
            }
            default { throw "Unknown argument $argument. $($script:Usage)" }
        }
        $index++
    }
    return $options
}

function Read-ChangelogSource($Options) {
    if (-not $Options.File) {
        return Read-FileFromHistory -RepositoryPath (Split-Path -Parent $PSScriptRoot) -Path $script:ChangelogFileName
    }
    if (-not (Test-Path -LiteralPath $Options.File -PathType Leaf)) {
        throw "Changelog file not found: $($Options.File)"
    }
    $path = (Resolve-Path -LiteralPath $Options.File).ProviderPath
    $text = (New-Object System.Text.UTF8Encoding($false)).GetString([IO.File]::ReadAllBytes($path))
    return $text.TrimStart([char]0xFEFF)
}

function Write-MigrationList([string]$Title, $Items) {
    $names = @($Items)
    $listed = if ($names.Count -eq 0) { 'none' } else { $names -join ', ' }
    Write-Host "${Title}: $listed"
}

function Update-ReleaseBody([string]$Repository, $Update) {
    $path = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($path, $Update.Body, (New-Object System.Text.UTF8Encoding($false)))
        Invoke-NativeChecked -FilePath 'gh' -ArgumentList @('api', '-X', 'PATCH', "repos/$Repository/releases/$($Update.Id)", '-F', "body=@$path") | Out-Null
    }
    finally {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-ReleaseUpdate([string]$Repository, $Update, [bool]$Apply) {
    if (-not $Apply) {
        Write-Host "Would update $($Update.Tag)"
        Write-Host $Update.Block
        return
    }
    Write-Host "Updating $($Update.Tag)"
    Write-Host $Update.Block
    Update-ReleaseBody -Repository $Repository -Update $Update
    Write-Host "Updated $($Update.Tag)"
}

function Main($Arguments) {
    $options = Read-MigrationArguments $Arguments
    $repository = Get-GitHubRepository
    $sections = @(ConvertFrom-Changelog -Text (Read-ChangelogSource $options))
    $releases = @(Get-GitHubRelease -Repository $repository)
    $plan = Get-ChangelogMigrationPlan -Sections $sections -Releases $releases

    foreach ($update in $plan.Updates) {
        Invoke-ReleaseUpdate -Repository $repository -Update $update -Apply $options.Apply
    }
    Write-MigrationList 'Up to date' $plan.UpToDate
    Write-MigrationList 'Empty changelog sections' $plan.Empty
    Write-MigrationList 'Changelog sections without a stable release' $plan.Unmatched
    if ($options.Apply) {
        Write-Host "Updated $($plan.Updates.Count) release(s)."
    }
    else {
        Write-Host "Dry run: $($plan.Updates.Count) release(s) would be updated. Pass --apply to write."
    }
}

Main $args
