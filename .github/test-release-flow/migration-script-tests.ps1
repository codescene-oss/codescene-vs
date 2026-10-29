function Get-MigrationScriptPath {
    return Join-Path $script:GitHubDirectory 'migrate-changelog-to-releases.ps1'
}

function Invoke-MigrationScript([string[]]$Arguments, [hashtable]$Environment = @{}) {
    return Invoke-EntryScript -Command '' -Environment $Environment -ScriptPath (Get-MigrationScriptPath) -ScriptArguments $Arguments
}

function New-FakeMigrationGh {
    $directory = New-TempDirectory
    $script = @'
$line = $args -join ' '
[IO.File]::AppendAllText($env:FAKE_GH_LOG, "$line`n")
if ($line -like 'api --paginate --slurp repos/*/releases*') { '[[' + $env:FAKE_RELEASES + ']]'; exit 0 }
if ($line -like 'api -X PATCH repos/*/releases/* -F body=@*') {
    $id = ($args[3] -split '/')[-1]
    $path = ([string]$args[5]).Substring(6)
    [IO.File]::WriteAllBytes((Join-Path $env:FAKE_GH_BODIES "$id.md"), [IO.File]::ReadAllBytes($path))
    '{}'
    exit 0
}
"unexpected gh call: $line"
exit 9
'@
    [IO.File]::WriteAllText((Join-Path $directory 'gh.ps1'), $script, (New-Object System.Text.UTF8Encoding($false)))
    return $directory
}

function Invoke-FakeMigration([string[]]$Arguments, $Releases) {
    $fake = New-FakeMigrationGh
    $bodies = New-TempDirectory
    $log = Join-Path (New-TempDirectory) 'gh.log'
    $json = (@($Releases) | ForEach-Object { $_ | ConvertTo-Json -Compress }) -join ','
    $variables = @{
        GITHUB_REPOSITORY = 'o/r'
        FAKE_GH_LOG       = $log
        FAKE_GH_BODIES    = $bodies
        FAKE_RELEASES     = $json
        PATH              = "$fake;$env:PATH"
    }
    $result = Invoke-MigrationScript $Arguments $variables
    $calls = @()
    if (Test-Path -LiteralPath $log) { $calls = @((Read-AllText $log) -split "`n" | Where-Object { $_ }) }
    return [pscustomobject]@{ Result = $result; Calls = $calls; Bodies = $bodies }
}

function Test-MigrationScriptArguments {
    $bogus = Invoke-MigrationScript @('--bogus')
    Assert-EntryScriptFails $bogus 'Unknown argument --bogus.*Usage' 'an unknown argument'

    $noValue = Invoke-MigrationScript @('--file')
    Assert-EntryScriptFails $noValue '--file requires a path' '--file without a value'

    $extra = Invoke-MigrationScript @('--apply', 'notes.md')
    Assert-EntryScriptFails $extra 'Unknown argument notes\.md' 'a stray positional argument'

    $missing = Invoke-MigrationScript @('--file', (Join-Path (New-TempDirectory) 'missing.md'))
    Assert-EntryScriptFails $missing 'Changelog file not found' '--file pointing at a missing file'
}

function Test-MigrationScriptWithFakeGh {
    $directory = New-TempDirectory
    $changelog = Join-Path $directory 'changelog.md'
    $bom = [string][char]0xFEFF
    Write-AllText $changelog ($bom + ($script:ChangelogFixture -replace "`n", "`r`n"))
    $sections = @(ConvertFrom-Changelog -Text $script:ChangelogFixture)
    $section091 = Get-ChangelogSectionBody $sections '0.9.1'
    $identical = Set-ChangelogBlock -Body 'Automated build for v0.9.0' -Section (Get-ChangelogSectionBody $sections '0.9.0')
    $releases = Get-MigrationFixtureReleases $identical

    $dry = Invoke-FakeMigration @('--file', $changelog) $releases
    Assert-Equal -Expected 0 -Actual $dry.Result.ExitCode -Name "a dry run exits zero ($($dry.Result.Output))"
    Assert-True -Condition ($dry.Result.Output -clike '*Would update v0.9.1*') -Name 'a dry run announces the update'
    Assert-True -Condition ($dry.Result.Output -clike '*<!-- changelog-migration:start -->*keep WebView2 visible (#341)*<!-- changelog-migration:end -->*') -Name 'a dry run prints the block'
    Assert-True -Condition ($dry.Result.Output -clike '*Up to date: v0.9.0*') -Name 'a dry run lists the up to date releases'
    Assert-True -Condition ($dry.Result.Output -clike '*Empty changelog sections: 0.8.7*') -Name 'a dry run lists the empty sections'
    Assert-True -Condition ($dry.Result.Output -clike '*Changelog sections without a stable release: Unreleased, 0.8.1, Notes*') -Name 'a dry run lists the unmatched sections'
    Assert-True -Condition ($dry.Result.Output -clike '*Dry run: 1 release(s) would be updated. Pass --apply to write.*') -Name 'a dry run prints the summary'
    Assert-Equal -Expected 0 -Actual @($dry.Calls | Where-Object { $_ -clike '*PATCH*' }).Count -Name 'a dry run issues no PATCH'
    Assert-Equal -Expected 0 -Actual @(Get-ChildItem -LiteralPath $dry.Bodies -Force).Count -Name 'a dry run writes no body'

    $applied = Invoke-FakeMigration @('--file', $changelog, '--apply') $releases
    Assert-Equal -Expected 0 -Actual $applied.Result.ExitCode -Name "an apply run exits zero ($($applied.Result.Output))"
    Assert-True -Condition ($applied.Result.Output -clike '*Updating v0.9.1*Updated v0.9.1*') -Name 'an apply run reports the update'
    Assert-True -Condition (-not ($applied.Result.Output -clike '*Dry run*')) -Name 'an apply run prints no dry run summary'
    $patches = @($applied.Calls | Where-Object { $_ -clike '*PATCH*' })
    Assert-Equal -Expected 1 -Actual $patches.Count -Name 'an apply run patches exactly the one outdated release'
    Assert-True -Condition ($patches[0] -clike 'api -X PATCH repos/o/r/releases/101 -F body=@*') -Name 'the PATCH targets the release id with a body file'
    Assert-Equal -Expected 0 -Actual @($applied.Calls | Where-Object { $_ -clike 'release create*' -or $_ -clike '*-X POST*' }).Count -Name 'the migration never creates releases'

    $bodyPath = Join-Path $applied.Bodies '101.md'
    Assert-True -Condition (Test-Path -LiteralPath $bodyPath) -Name 'the patched body file reached gh'
    $bytes = [IO.File]::ReadAllBytes($bodyPath)
    Assert-True -Condition (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) -Name 'the body file has no byte order mark'
    Assert-Equal -Expected (Set-ChangelogBlock -Body 'Automated build for v0.9.1' -Section $section091) -Actual (Read-AllText $bodyPath) -Name 'the body file holds the new release body'
    $tempFile = $patches[0].Substring($patches[0].IndexOf('body=@') + 6)
    Assert-True -Condition (-not (Test-Path -LiteralPath $tempFile)) -Name 'the temporary body file is removed afterwards'

    $migrated = @(
        (Get-FixtureRelease 'v0.9.1' (Set-ChangelogBlock -Body 'Automated build for v0.9.1' -Section $section091) $false $false 101),
        (Get-FixtureRelease 'v0.9.0' $identical $false $false 100)
    )
    $again = Invoke-FakeMigration @('--file', $changelog, '--apply') $migrated
    Assert-Equal -Expected 0 -Actual $again.Result.ExitCode -Name 'a second apply run exits zero'
    Assert-Equal -Expected 0 -Actual @($again.Calls | Where-Object { $_ -clike '*PATCH*' }).Count -Name 'a second apply run patches nothing'
    Assert-True -Condition ($again.Result.Output -clike '*Up to date: v0.9.1, v0.9.0*') -Name 'a second apply run finds everything up to date'
}

function Test-MigrationIntegration {
    $token = Get-GitHubTokenForTests
    if (-not $token) {
        Write-Host 'skipping migration integration: no GitHub token available'
        return
    }
    $run = Invoke-MigrationScript @() @{ GH_TOKEN = $token }
    Assert-Equal -Expected 0 -Actual $run.ExitCode -Name "the migration dry run against the real releases exits zero ($($run.Output))"
    foreach ($tag in @('v0.9.1', 'v0.9.0', 'v0.7.0', 'v0.3.0', 'v0.2.7', 'v0.2.0')) {
        Assert-True -Condition ($run.Output -clike "*Would update $tag`n*") -Name "the dry run would update $tag"
    }
    Assert-True -Condition ($run.Output -clike '*Changelog sections without a stable release: Unreleased, *0.1.4*0.0.1*') -Name 'the dry run reports Unreleased and 0.0.1 to 0.1.x as unmatched'
    Assert-True -Condition ($run.Output -match 'Dry run: \d+ release\(s\) would be updated\. Pass --apply to write\.') -Name 'the dry run prints the summary'
}
