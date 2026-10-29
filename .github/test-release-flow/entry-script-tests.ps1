function Get-EntryScriptPath {
    return Join-Path $script:GitHubDirectory 'release-flow.ps1'
}

function Invoke-EntryScript {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Command,
        [hashtable]$Environment = @{},
        [string]$WorkingDirectory = $script:RepositoryRoot,
        [string]$ScriptPath = (Get-EntryScriptPath),
        [string[]]$ScriptArguments = @()
    )

    $names = @('TAG', 'EVENT', 'RUN_REF', 'DRY_RUN', 'VERSION', 'REPO_PATH', 'GITHUB_OUTPUT', 'GITHUB_REPOSITORY', 'GH_TOKEN', 'GITHUB_TOKEN', 'GH_REPO', 'KIND', 'NOTES_START', 'SHA', 'ASSET_DIR', 'GITHUB_SERVER_URL', 'RUNNER_TEMP', 'FAKE_GH_LOG', 'FAKE_RELEASE_EXISTS', 'FAKE_ASSETS', 'FAKE_LATEST', 'FAKE_RUNS', 'VSIX_PUBLISHER', 'VSIX_DIR', 'PUBLISH_MANIFEST', 'FAKE_PUBLISH_LOG', 'FAKE_PUBLISH_EXIT', 'FAKE_PUBLISH_OUTPUT')
    $restored = @($names) + @($Environment.Keys | Where-Object { $names -cnotcontains $_ })
    $saved = @{}
    foreach ($name in $restored) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    Push-Location -LiteralPath $WorkingDirectory
    try {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $null) }
        foreach ($name in $Environment.Keys) { [Environment]::SetEnvironmentVariable($name, [string]$Environment[$name]) }
        $shell = (Get-Process -Id $PID).Path
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ScriptPath)
        if ($Command) { $arguments += $Command }
        $arguments += @($ScriptArguments)
        return Invoke-Native -FilePath $shell -ArgumentList $arguments
    }
    finally {
        Pop-Location
        foreach ($name in $restored) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
}

function Read-ActionOutput([string]$Path) {
    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $values }
    foreach ($line in @((Read-AllText $Path) -split "`n" | Where-Object { $_ })) {
        $index = $line.IndexOf('=')
        $values[$line.Substring(0, $index)] = $line.Substring($index + 1)
    }
    return $values
}

function Assert-EntryScriptFails($Result, [string]$Pattern, [string]$Name) {
    Assert-True -Condition ($Result.ExitCode -ne 0) -Name "$Name exits non-zero"
    $text = ($Result.Output -replace "$([char]27)\[[0-9;]*m", '') -replace '\s+', ' '
    Assert-True -Condition ($text -match $Pattern) -Name "$Name reports '$Pattern'"
}

function Test-EntryScriptUsage {
    $unknown = Invoke-EntryScript -Command 'frobnicate'
    Assert-EntryScriptFails $unknown "Unknown command 'frobnicate'.*metadata\|set-version\|verify-vsix" 'an unknown command'

    $empty = Invoke-EntryScript -Command ''
    Assert-EntryScriptFails $empty 'Unknown command.*Usage' 'no command'

    $wrongCase = Invoke-EntryScript -Command 'Metadata'
    Assert-EntryScriptFails $wrongCase 'Unknown command' 'a differently cased command'

    $noToken = Invoke-EntryScript -Command 'metadata' -Environment @{ TAG = 'v0.9.1'; EVENT = 'push' }
    Assert-EntryScriptFails $noToken 'GH_TOKEN is required' 'metadata without a token'

    $noTag = Invoke-EntryScript -Command 'metadata' -Environment @{ GH_TOKEN = 'unused'; EVENT = 'push' }
    Assert-EntryScriptFails $noTag 'TAG is required' 'metadata without a tag'

    $malformed = Invoke-EntryScript -Command 'metadata' -Environment @{ GH_TOKEN = 'unused'; EVENT = 'push'; TAG = 'v1.2' }
    Assert-EntryScriptFails $malformed 'Malformed tag' 'metadata with a malformed tag'

    $noVersion = Invoke-EntryScript -Command 'set-version'
    Assert-EntryScriptFails $noVersion 'VERSION is required' 'set-version without a version'

    $noVerifyVersion = Invoke-EntryScript -Command 'verify-vsix'
    Assert-EntryScriptFails $noVerifyVersion 'VERSION is required' 'verify-vsix without a version'

    foreach ($command in @('create-release', 'upload', 'download-asset', 'test-gate', 'promote')) {
        $result = Invoke-EntryScript -Command $command
        Assert-EntryScriptFails $result 'GH_TOKEN is required' "$command without a token"
    }
}
