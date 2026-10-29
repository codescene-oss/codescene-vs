function Get-TagScriptPath {
    return Join-Path $script:RepositoryRoot 'tag-test-release.ps1'
}

function Invoke-GitChecked([string]$Directory, [string[]]$Arguments) {
    return (Invoke-NativeChecked -FilePath 'git' -ArgumentList (@('-C', $Directory, '-c', 'user.name=release-flow-test', '-c', 'user.email=release-flow-test@example.com') + $Arguments)).Output
}

function New-TagScriptRepository {
    $source = $script:RepositoryRoot
    $origin = Join-Path (New-TempDirectory) 'origin.git'
    $work = Join-Path (New-TempDirectory) 'work'
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('clone', '--bare', '--local', '--quiet', $source, $origin) | Out-Null
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('clone', '--local', '--quiet', $origin, $work) | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $work '.github') -Force | Out-Null
    Copy-Item -LiteralPath (Get-TagScriptPath) -Destination (Join-Path $work 'tag-test-release.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $script:GitHubDirectory 'release-flow.psm1') -Destination (Join-Path $work '.github\release-flow.psm1') -Force
    Copy-Item -LiteralPath (Join-Path $script:GitHubDirectory 'release-flow') -Destination (Join-Path $work '.github') -Recurse -Force
    Invoke-GitChecked $work @('add', 'tag-test-release.ps1', '.github/release-flow.psm1', '.github/release-flow') | Out-Null
    Invoke-GitChecked $work @('commit', '--quiet', '--allow-empty', '-m', 'test fixture') | Out-Null
    $head = Invoke-GitChecked $work @('rev-parse', 'HEAD')
    return [pscustomobject]@{ Work = $work; Origin = $origin; Head = $head }
}

function Invoke-TagScript {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [string[]]$Arguments = @(),
        [hashtable]$Environment = @{}
    )

    $cleared = @('GH_TOKEN', 'GITHUB_TOKEN', 'GH_REPO', 'FAKE_GH_LOG', 'FAKE_AUTH', 'FAKE_RELEASES')
    $restored = @($cleared) + @($Environment.Keys | Where-Object { $cleared -cnotcontains $_ })
    $saved = @{}
    foreach ($name in $restored) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        foreach ($name in $cleared) { [Environment]::SetEnvironmentVariable($name, $null) }
        foreach ($name in $Environment.Keys) { [Environment]::SetEnvironmentVariable($name, [string]$Environment[$name]) }
        $shell = (Get-Process -Id $PID).Path
        $script = Join-Path $Directory 'tag-test-release.ps1'
        return Invoke-Native -FilePath $shell -ArgumentList (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $script) + $Arguments)
    }
    finally {
        foreach ($name in $restored) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
}

function Invoke-TagScriptWithFakeGh {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [string[]]$Arguments = @(),
        [string]$Releases = '',
        [string]$Auth = 'ok'
    )

    $fake = New-FakeGh
    $log = Join-Path (New-TempDirectory) 'gh.log'
    $environment = @{ FAKE_GH_LOG = $log; FAKE_AUTH = $Auth; FAKE_RELEASES = $Releases; PATH = "$fake;$env:PATH" }
    $result = Invoke-TagScript -Directory $Directory -Arguments $Arguments -Environment $environment
    $calls = @()
    if (Test-Path -LiteralPath $log) { $calls = @((Read-AllText $log) -split "`n" | Where-Object { $_ }) }
    return [pscustomobject]@{ Result = $result; Calls = $calls }
}

function Get-TagScriptReleases {
    $items = @(
        '{"tag_name":"v0.9.1","draft":false,"prerelease":false}',
        '{"tag_name":"v0.9.0","draft":false,"prerelease":false}',
        '{"tag_name":"v0.9.2-test.abc1234","draft":false,"prerelease":true}',
        '{"tag_name":"v1.0.0","draft":true,"prerelease":false}'
    )
    return $items -join ','
}

function Get-FixtureTagList($Fixture, [string]$Side) {
    return Invoke-GitChecked $Fixture.$Side @('tag', '-l', '--points-at', $Fixture.Head)
}

function Test-TagTestReleaseScriptPrerequisites {
    $emptyPath = New-TempDirectory
    $work = New-TempDirectory
    Copy-Item -LiteralPath (Get-TagScriptPath) -Destination (Join-Path $work 'tag-test-release.ps1')
    $result = Invoke-TagScript -Directory $work -Arguments @('-WhatIf') -Environment @{ PATH = $emptyPath }
    Assert-EntryScriptFails $result 'GitHub CLI \(gh\) is required' 'tag-test-release without gh'
    Assert-True -Condition ($result.Output -notmatch 'not a git repository') -Name 'tag-test-release without gh fails before any git call'

    $fixture = New-TagScriptRepository
    $unauthenticated = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases) -Auth 'fail'
    Assert-EntryScriptFails $unauthenticated.Result 'gh is not authenticated' 'tag-test-release with gh logged out'
    Assert-Equal -Expected 'auth status' -Actual ($unauthenticated.Calls -join '|') -Name 'tag-test-release with gh logged out stops after the auth check'

    Set-Content -LiteralPath (Join-Path $fixture.Work 'stray.txt') -Value 'dirty'
    $dirty = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $dirty.Result 'requires a clean worktree' 'tag-test-release with an untracked file'
    Assert-EntryScriptFails $dirty.Result 'stray\.txt' 'tag-test-release names the offending file'
    Assert-Equal -Expected 'auth status' -Actual ($dirty.Calls -join '|') -Name 'tag-test-release with a dirty worktree does not load releases'
    Remove-Item -LiteralPath (Join-Path $fixture.Work 'stray.txt')

    Add-Content -LiteralPath (Join-Path $fixture.Work 'tag-test-release.ps1') -Value '# edited'
    $modified = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $modified.Result 'requires a clean worktree' 'tag-test-release with a modified tracked file'
    Invoke-GitChecked $fixture.Work @('checkout', '--', 'tag-test-release.ps1') | Out-Null

    $bogus = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf', '-Bump', 'bogus') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $bogus.Result 'does not belong to the set' 'tag-test-release rejects an unknown bump'
    Assert-Equal -Expected 0 -Actual $bogus.Calls.Count -Name 'tag-test-release rejects an unknown bump before calling gh'
}

function Test-TagTestReleaseScriptReleases {
    $fixture = New-TagScriptRepository
    $none = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases ''
    Assert-EntryScriptFails $none.Result 'No published stable release found' 'tag-test-release with no releases'

    $unpublished = '{"tag_name":"v0.9.2-test.abc1234","draft":false,"prerelease":true},{"tag_name":"v1.0.0","draft":true,"prerelease":false},{"tag_name":"v1.1.0","draft":false,"prerelease":true}'
    $onlyUnpublished = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases $unpublished
    Assert-EntryScriptFails $onlyUnpublished.Result 'No published stable release found' 'tag-test-release with only test, draft and prerelease entries'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Work') -Name 'tag-test-release without releases tags nothing'

    $sha = Invoke-GitChecked $fixture.Work @('rev-parse', '--short=7', 'HEAD')
    $expected = @{ patch = '0.9.2'; minor = '0.10.0'; major = '1.0.0' }
    foreach ($bump in @('patch', 'minor', 'major')) {
        $run = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf', '-Bump', $bump) -Releases (Get-TagScriptReleases)
        $version = $expected[$bump]
        Assert-Equal -Expected 0 -Actual $run.Result.ExitCode -Name "tag-test-release -WhatIf -Bump $bump exits zero ($($run.Result.Output))"
        Assert-Equal -Expected "Would create tag: v$version-test.$sha (next version $version after v0.9.1)" -Actual $run.Result.Output.Trim() -Name "tag-test-release -WhatIf -Bump $bump output"
    }

    $default = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-Equal -Expected "Would create tag: v0.9.2-test.$sha (next version 0.9.2 after v0.9.1)" -Actual $default.Result.Output.Trim() -Name 'tag-test-release defaults to a patch bump'

    $longSha = Invoke-GitChecked $fixture.Work @('rev-parse', '--short=10', 'HEAD')
    $long = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf', '-ShaLength', '10') -Releases (Get-TagScriptReleases)
    Assert-Equal -Expected "Would create tag: v0.9.2-test.$longSha (next version 0.9.2 after v0.9.1)" -Actual $long.Result.Output.Trim() -Name 'tag-test-release honours -ShaLength'

    $short = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf', '-ShaLength', '4') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $short.Result 'Malformed tag' 'tag-test-release with a sha length below seven'

    $ordering = '{"tag_name":"v0.9.9","draft":false,"prerelease":false},{"tag_name":"v0.10.0","draft":false,"prerelease":false}'
    $numeric = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases $ordering
    Assert-Equal -Expected "Would create tag: v0.10.1-test.$sha (next version 0.10.1 after v0.10.0)" -Actual $numeric.Result.Output.Trim() -Name 'tag-test-release compares versions numerically'

    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Work') -Name 'tag-test-release -WhatIf creates no local tag'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Origin') -Name 'tag-test-release -WhatIf pushes nothing'
}

function Test-TagTestReleaseScriptTagging {
    $fixture = New-TagScriptRepository
    $sha = Invoke-GitChecked $fixture.Work @('rev-parse', '--short=7', 'HEAD')
    $head = Invoke-GitChecked $fixture.Work @('rev-parse', 'HEAD')
    $tag = "v0.9.2-test.$sha"

    $noPush = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-NoPush') -Releases (Get-TagScriptReleases)
    Assert-Equal -Expected 0 -Actual $noPush.Result.ExitCode -Name "tag-test-release -NoPush exits zero ($($noPush.Result.Output))"
    Assert-Equal -Expected $tag -Actual (Get-FixtureTagList $fixture 'Work') -Name 'tag-test-release -NoPush creates the local tag'
    Assert-Equal -Expected 'tag' -Actual (Invoke-GitChecked $fixture.Work @('cat-file', '-t', $tag)) -Name 'the test tag is annotated'
    Assert-Equal -Expected $head -Actual (Invoke-GitChecked $fixture.Work @('rev-parse', "$tag^{commit}")) -Name 'the test tag points at HEAD'
    Assert-Equal -Expected "Test release 0.9.2 ($sha)" -Actual (Invoke-GitChecked $fixture.Work @('tag', '-l', '--format=%(contents:subject)', $tag)) -Name 'the test tag message'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Origin') -Name 'tag-test-release -NoPush leaves origin untouched'
    Assert-True -Condition ($noPush.Result.Output -match 'Skipped push') -Name 'tag-test-release -NoPush reports the skipped push'

    $again = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $again.Result "Tag '$tag' already exists locally" 'tag-test-release with the tag present locally'

    Invoke-GitChecked $fixture.Work @('tag', '-d', $tag) | Out-Null
    $pushed = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Releases (Get-TagScriptReleases)
    Assert-Equal -Expected 0 -Actual $pushed.Result.ExitCode -Name "tag-test-release pushes by default ($($pushed.Result.Output))"
    Assert-Equal -Expected $tag -Actual (Get-FixtureTagList $fixture 'Origin') -Name 'tag-test-release pushes the tag to origin'

    Invoke-GitChecked $fixture.Work @('tag', '-d', $tag) | Out-Null
    $remote = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $remote.Result "Tag '$tag' already exists on origin" 'tag-test-release with the tag present on origin'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Work') -Name 'tag-test-release with the tag on origin creates nothing locally'

    Invoke-GitChecked $fixture.Work @('remote', 'set-url', 'origin', (Join-Path (New-TempDirectory) 'missing.git')) | Out-Null
    $unreachable = Invoke-TagScriptWithFakeGh -Directory $fixture.Work -Arguments @('-WhatIf') -Releases (Get-TagScriptReleases)
    Assert-EntryScriptFails $unreachable.Result 'Failed to check remote' 'tag-test-release with an unreachable origin'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Work') -Name 'tag-test-release with an unreachable origin creates nothing'
}

function Test-TagTestReleaseScriptIntegration {
    $token = Get-GitHubTokenForTests
    if (-not $token) {
        Write-Host 'skipping tag-test-release GitHub integration: no GitHub token available'
        return
    }
    $env:GH_TOKEN = $token
    try {
        $releases = @(Get-GitHubRelease -Repository 'codescene-oss/codescene-vs')
    }
    finally {
        $env:GH_TOKEN = $null
    }
    $highest = Select-HighestStableRelease -Releases $releases
    if (-not $highest) {
        Write-Host 'skipping tag-test-release GitHub integration: no published stable release'
        return
    }
    $current = (Get-ReleaseTagInfo -Tag $highest.tag_name).Version
    $fixture = New-TagScriptRepository
    $sha = Invoke-GitChecked $fixture.Work @('rev-parse', '--short=7', 'HEAD')
    foreach ($bump in @('patch', 'minor', 'major')) {
        $next = Get-NextReleaseVersion -Version $current -Bump $bump
        $run = Invoke-TagScript -Directory $fixture.Work -Arguments @('-WhatIf', '-Bump', $bump) -Environment @{ GH_TOKEN = $token }
        Assert-Equal -Expected 0 -Actual $run.ExitCode -Name "real tag-test-release -WhatIf -Bump $bump exits zero ($($run.Output))"
        Assert-Equal -Expected "Would create tag: v$next-test.$sha (next version $next after $($highest.tag_name))" -Actual $run.Output.Trim() -Name "real tag-test-release -WhatIf -Bump $bump output"
    }
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Work') -Name 'real tag-test-release -WhatIf creates no local tag'
    Assert-Equal -Expected '' -Actual (Get-FixtureTagList $fixture 'Origin') -Name 'real tag-test-release -WhatIf pushes nothing'
}
