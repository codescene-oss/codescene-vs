function New-RecordFixture {
    $work = New-TempDirectory
    $origin = Join-Path $work 'origin.git'
    $seed = New-VersionRepository
    Set-ReleaseVersion -RepositoryPath $seed -Version '0.9.1'
    $clone = Join-Path $work 'clone'
    $identity = @('-c', 'user.name=Seeder', '-c', 'user.email=seed@example.com')
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('init', '--bare', '--initial-branch=main', $origin) | Out-Null
    Invoke-GitChecked $seed @('init', '--initial-branch=main') | Out-Null
    Invoke-GitChecked $seed @('add', '-A') | Out-Null
    Invoke-GitChecked $seed ($identity + @('commit', '-m', 'seed')) | Out-Null
    Invoke-GitChecked $seed @('push', $origin, 'main') | Out-Null
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('clone', $origin, $clone) | Out-Null
    return [pscustomobject]@{ Work = $work; Origin = $origin; Clone = $clone }
}

function Set-OriginHook($Fixture, [string]$Body) {
    $hook = Join-Path $Fixture.Origin 'hooks/pre-receive'
    [IO.File]::WriteAllBytes($hook, (New-Object System.Text.UTF8Encoding($false)).GetBytes("#!/bin/sh`n$Body`n"))
}

function Get-OriginCommitCount($Fixture) {
    return [int](Invoke-GitChecked $Fixture.Origin @('rev-list', '--count', 'main'))
}

function Test-VersionRecordIntegration {
    $fixture = New-RecordFixture
    $before = Get-OriginCommitCount $fixture
    $record = Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version '0.9.2'
    Assert-Equal -Expected $true -Actual $record.Changed -Name 'recording a higher version changes main'
    Assert-Equal -Expected 1 -Actual $record.Attempt -Name 'recording a higher version succeeds on the first attempt'
    Assert-Equal -Expected ($before + 1) -Actual (Get-OriginCommitCount $fixture) -Name 'origin gains exactly one commit'
    Assert-Equal -Expected 'github-actions[bot]|41898282+github-actions[bot]@users.noreply.github.com|chore(release): set version to 0.9.2' -Actual (Invoke-GitChecked $fixture.Origin @('log', '-1', '--format=%an|%ae|%s', 'main')) -Name 'the origin commit has the bot author and message'
    Assert-Equal -Expected 'github-actions[bot]' -Actual (Invoke-GitChecked $fixture.Origin @('log', '-1', '--format=%cn', 'main')) -Name 'the origin commit has the bot committer'
    $changed = @((Invoke-GitChecked $fixture.Origin @('diff', '--name-only', 'main~1', 'main')) -split "`n" | Sort-Object)
    Assert-Equal -Expected 'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.cs|Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.vsixmanifest' -Actual ($changed -join '|') -Name 'the origin commit changes exactly the two version files'
    $oneLine = @((Invoke-GitChecked $fixture.Origin @('diff', '--numstat', 'main~1', 'main')) -split "`n" | ForEach-Object { ($_ -split "`t")[0] })
    Assert-Equal -Expected '1|1' -Actual ($oneLine -join '|') -Name 'each version file gains and loses exactly one line'
    Assert-Equal -Expected '0.9.2' -Actual (Get-ReleaseVersion -RepositoryPath $fixture.Clone) -Name 'the clone carries the recorded version'
    Assert-Equal -Expected '' -Actual (Invoke-GitChecked $fixture.Clone @('status', '--porcelain')) -Name 'the clone is clean after recording'

    $again = Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version '0.9.2'
    Assert-Equal -Expected $false -Actual $again.Changed -Name 'recording the same version again changes nothing'
    Assert-Equal -Expected ($before + 1) -Actual (Get-OriginCommitCount $fixture) -Name 'recording the same version again adds no commit'

    $lower = Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version '0.9.0'
    Assert-Equal -Expected $false -Actual $lower.Changed -Name 'recording a lower version changes nothing'
    Assert-Equal -Expected '0.9.2' -Actual (Get-ReleaseVersion -RepositoryPath $fixture.Clone) -Name 'a lower version never lowers main'
    Assert-Equal -Expected ($before + 1) -Actual (Get-OriginCommitCount $fixture) -Name 'recording a lower version adds no commit'

    Assert-Throws -Action { Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version 'v0.9.3' } -MessagePattern 'Malformed version' -Name 'a malformed version is rejected before touching git'
}

function Test-VersionRecordSyncsWithOrigin {
    $fixture = New-RecordFixture
    $other = Join-Path $fixture.Work 'other'
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('clone', $fixture.Origin, $other) | Out-Null
    Invoke-VersionRecord -RepositoryPath $other -Version '0.9.5' | Out-Null
    $stale = Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version '0.9.3'
    Assert-Equal -Expected $false -Actual $stale.Changed -Name 'a stale clone is brought up to origin before deciding'
    Assert-Equal -Expected '0.9.5' -Actual (Get-ReleaseVersion -RepositoryPath $fixture.Clone) -Name 'the stale clone now carries the version recorded by another run'
}

function Test-VersionRecordStrayFile {
    $fixture = New-RecordFixture
    Write-AllText (Join-Path $fixture.Clone 'stray.txt') 'x'
    $count = Get-OriginCommitCount $fixture
    Assert-Throws -Action { Invoke-VersionRecord -RepositoryPath $fixture.Clone -Version '0.9.2' } -MessagePattern 'stray\.txt' -Name 'an untracked file stops the version record'
    Assert-Equal -Expected $count -Actual (Get-OriginCommitCount $fixture) -Name 'a stray file adds no origin commit'
}

function Test-VersionRecordRetry {
    $once = New-RecordFixture
    $marker = (Join-Path $once.Work 'rejected-once').Replace('\', '/')
    Set-OriginHook $once "if [ -f '$marker' ]; then exit 0; fi`ntouch '$marker'`necho 'test hook rejects the first push' >&2`nexit 1"
    $before = Get-OriginCommitCount $once
    $record = Invoke-VersionRecord -RepositoryPath $once.Clone -Version '0.9.2'
    Assert-Equal -Expected $true -Actual $record.Changed -Name 'a rejected first push is retried to success'
    Assert-Equal -Expected 2 -Actual $record.Attempt -Name 'the retry succeeds on attempt 2'
    Assert-Equal -Expected ($before + 1) -Actual (Get-OriginCommitCount $once) -Name 'a retried record adds exactly one origin commit'
    Assert-Equal -Expected '0.9.2' -Actual (Get-ReleaseVersion -RepositoryPath $once.Clone) -Name 'the retried record leaves the version recorded'

    $always = New-RecordFixture
    Set-OriginHook $always "echo 'test hook rejects every push' >&2`nexit 1"
    $count = Get-OriginCommitCount $always
    Assert-Throws -Action { Invoke-VersionRecord -RepositoryPath $always.Clone -Version '0.9.2' -Attempts 2 } -MessagePattern 'Could not record version 0\.9\.2 on main after 2 attempts' -Name 'an always-rejecting origin exhausts the attempts'
    Assert-Equal -Expected $count -Actual (Get-OriginCommitCount $always) -Name 'an always-rejecting origin gains no commit'

    $racing = New-RecordFixture
    $racer = Join-Path $racing.Work 'racer'
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('clone', $racing.Origin, $racer) | Out-Null
    Write-AllText (Join-Path $racer 'other.txt') 'unrelated'
    Invoke-GitChecked $racer @('add', '-A') | Out-Null
    Invoke-GitChecked $racer @('-c', 'user.name=Racer', '-c', 'user.email=racer@example.com', 'commit', '-m', 'unrelated change') | Out-Null
    Invoke-GitChecked $racer @('push', 'origin', 'main') | Out-Null
    $viaRace = Invoke-VersionRecord -RepositoryPath $racing.Clone -Version '0.9.2'
    Assert-Equal -Expected $true -Actual $viaRace.Changed -Name 'a clone behind origin still records the version'
    Assert-Equal -Expected 'unrelated' -Actual (Read-AllText (Join-Path $racing.Clone 'other.txt')) -Name 'the record is made on top of the newest origin main'

    $unreachable = New-RecordFixture
    Invoke-GitChecked $unreachable.Clone @('remote', 'set-url', 'origin', (Join-Path $unreachable.Work 'no-such-origin.git')) | Out-Null
    Assert-Throws -Action { Invoke-VersionRecord -RepositoryPath $unreachable.Clone -Version '0.9.2' } -MessagePattern 'fetch' -Name 'an unreachable origin fails the fetch instead of retrying'

    $denied = New-RecordFixture
    Invoke-GitChecked $denied.Clone @('remote', 'set-url', '--push', 'origin', (Join-Path $denied.Work 'no-such-origin.git')) | Out-Null
    Assert-Throws -Action { Invoke-VersionRecord -RepositoryPath $denied.Clone -Version '0.9.2' -Attempts 3 } -MessagePattern 'git push origin HEAD:refs/heads/main failed' -Name 'a push failure that is not a rejection throws immediately'
}

function Test-RecordVersionCommand {
    $fixture = New-RecordFixture
    $noVersion = Invoke-EntryScript -Command 'record-version' -Environment @{ REPO_PATH = $fixture.Clone }
    Assert-EntryScriptFails $noVersion 'VERSION is required' 'record-version without a version'

    $run = Invoke-EntryScript -Command 'record-version' -Environment @{ VERSION = '0.9.2'; REPO_PATH = $fixture.Clone } -WorkingDirectory (New-TempDirectory)
    Assert-Equal -Expected 0 -Actual $run.ExitCode -Name "record-version exits zero ($($run.Output))"
    Assert-Equal -Expected 'chore(release): set version to 0.9.2' -Actual (Invoke-GitChecked $fixture.Origin @('log', '-1', '--format=%s', 'main')) -Name 'record-version pushes the version commit'

    $again = Invoke-EntryScript -Command 'record-version' -Environment @{ VERSION = '0.9.2'; REPO_PATH = $fixture.Clone }
    Assert-Equal -Expected 0 -Actual $again.ExitCode -Name 'record-version of the recorded version exits zero'
    Assert-True -Condition ($again.Output -clike '*main already at 0.9.2*') -Name 'record-version logs that main is already at the version'

    $bad = Invoke-EntryScript -Command 'record-version' -Environment @{ VERSION = '1.2'; REPO_PATH = $fixture.Clone }
    Assert-EntryScriptFails $bad 'Malformed version' 'record-version with a malformed version'
}
