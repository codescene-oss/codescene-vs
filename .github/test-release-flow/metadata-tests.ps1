function Get-GitHubTokenForTests {
    foreach ($candidate in @($env:GH_TOKEN, $env:GITHUB_TOKEN)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) { return $candidate }
    }
    try {
        $result = Invoke-Native -FilePath 'gh' -ArgumentList @('auth', 'token')
    }
    catch {
        return $null
    }
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Output)) { return $null }
    return $result.Output.Trim()
}

function Get-RefProblem([string]$Ref, [string]$Label, [string]$ExpectedPrefix = '') {
    $commit = Get-TagCommit -Tag $Ref
    if (-not $commit) { return "$Label is not available locally" }
    if ($ExpectedPrefix -and -not $commit.StartsWith($ExpectedPrefix, [StringComparison]::Ordinal)) { return "$Label does not point at $ExpectedPrefix" }
    return $null
}

function Test-MetadataPrerequisites {
    $problems = @(
        (Get-RefProblem 'v0.9.1' 'tag v0.9.1' '01f198db'),
        (Get-RefProblem 'v0.9.0-test.015c6a0' 'tag v0.9.0-test.015c6a0'),
        (Get-RefProblem '01f198db' 'commit 01f198db'),
        (Get-RefProblem '7f9f1296' 'commit 7f9f1296'),
        (Get-RefProblem 'origin/main' 'origin/main')
    )
    return $problems | Where-Object { $_ } | Select-Object -First 1
}

function Invoke-MetadataCommand([hashtable]$Environment, [string]$Token) {
    $output = Join-Path (New-TempDirectory) 'output.txt'
    $variables = @{ GH_TOKEN = $Token; GITHUB_OUTPUT = $output }
    foreach ($name in $Environment.Keys) { $variables[$name] = $Environment[$name] }
    $result = Invoke-EntryScript -Command 'metadata' -Environment $variables
    return [pscustomobject]@{ Result = $result; Outputs = (Read-ActionOutput $output) }
}

function Assert-MetadataFails($Run, [string]$Pattern, [string]$Name) {
    Assert-EntryScriptFails $Run.Result $Pattern $Name
    Assert-Equal -Expected 0 -Actual $Run.Outputs.Count -Name "$Name writes no outputs"
}

function Assert-MetadataPasses($Run, [hashtable]$Expected, [string]$Name) {
    Assert-Equal -Expected 0 -Actual $Run.Result.ExitCode -Name "$Name exits zero ($($Run.Result.Output))"
    foreach ($key in $Expected.Keys) {
        Assert-Equal -Expected $Expected[$key] -Actual $Run.Outputs[$key] -Name "$Name output $key"
    }
    Assert-Equal -Expected 6 -Actual $Run.Outputs.Count -Name "$Name writes all six outputs"
}

function Add-LocalTag([string]$Tag, [string]$Commit) {
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('tag', $Tag, $Commit) | Out-Null
    $script:LocalTags += $Tag
}

function Remove-LocalTags {
    foreach ($tag in $script:LocalTags) {
        Invoke-Native -FilePath 'git' -ArgumentList @('tag', '-d', $tag) | Out-Null
    }
    $script:LocalTags = @()
}

function Test-MetadataDispatch([string]$Token) {
    $main = @{ TAG = 'v0.9.1'; EVENT = 'workflow_dispatch'; RUN_REF = 'refs/heads/main' }
    $recovery = Invoke-MetadataCommand $main $Token
    Assert-MetadataPasses $recovery @{ tag = 'v0.9.1'; version = '0.9.1'; kind = 'stable'; notes_start = 'v0.9.0'; sha = '01f198db079248e461264ef79f94e82d8bd6faa8'; dry_run = 'false' } 'recovery dispatch of v0.9.1'
    Assert-True -Condition ($recovery.Result.Output -clike '*Comparison mode: recovery*') -Name 'a dispatch of a released tag logs the recovery mode'

    $dry = Invoke-MetadataCommand ($main + @{ DRY_RUN = 'true' }) $Token
    Assert-MetadataPasses $dry @{ version = '0.9.1'; dry_run = 'true' } 'dry run dispatch of v0.9.1'

    $notTrue = Invoke-MetadataCommand ($main + @{ DRY_RUN = 'True' }) $Token
    Assert-MetadataPasses $notTrue @{ dry_run = 'false' } 'a DRY_RUN other than true'

    $other = Invoke-MetadataCommand @{ TAG = 'v0.9.1'; EVENT = 'workflow_dispatch'; RUN_REF = 'refs/heads/other' } $Token
    Assert-MetadataFails $other 'must be dispatched from main' 'a dispatch from another branch'

    $missing = Invoke-MetadataCommand @{ TAG = 'v9.9.9'; EVENT = 'workflow_dispatch'; RUN_REF = 'refs/heads/main' } $Token
    Assert-MetadataFails $missing 'does not exist' 'a dispatch of a missing tag'

    $dryTest = Invoke-MetadataCommand @{ TAG = 'v0.9.0-test.015c6a0'; EVENT = 'workflow_dispatch'; RUN_REF = 'refs/heads/main'; DRY_RUN = 'true' } $Token
    Assert-MetadataFails $dryTest 'Dry run needs a stable tag' 'a dry run of a test tag'

    $unsupported = Invoke-MetadataCommand @{ TAG = 'v0.9.1'; EVENT = 'pull_request'; RUN_REF = 'refs/heads/main' } $Token
    Assert-MetadataFails $unsupported 'Unsupported event' 'an unsupported event'
}

function Test-MetadataFreshTags([string]$Token) {
    $highest = Select-HighestStableRelease -Releases @(Get-GitHubRelease -Repository 'codescene-oss/codescene-vs')
    if ($null -eq $highest -or $highest.tag_name -cne 'v0.9.1') {
        Write-Host 'skipping fresh tag checks: v0.9.1 is no longer the newest stable release'
        return
    }
    $headSha = (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('rev-parse', 'HEAD')).Output
    $headShort = $headSha.Substring(0, 7)
    $descendsFromRelease = Test-GitAncestor -Ancestor 'v0.9.1' -Descendant 'HEAD'
    $tags = @('v0.9.2', 'v0.9.3', "v0.9.2-test.$headShort", 'v0.9.2-test.abcdef1')
    foreach ($tag in $tags) {
        if (Get-TagCommit -Tag $tag) {
            Write-Host "skipping fresh tag checks: tag $tag already exists locally"
            return
        }
    }

    try {
        Add-LocalTag 'v0.9.2' '01f198db'
        Add-LocalTag 'v0.9.3' '7f9f1296'
        $fresh = Invoke-MetadataCommand @{ TAG = 'v0.9.2'; EVENT = 'push'; RUN_REF = 'refs/tags/v0.9.2' } $Token
        Assert-MetadataPasses $fresh @{ tag = 'v0.9.2'; version = '0.9.2'; kind = 'stable'; notes_start = 'v0.9.1'; sha = '01f198db079248e461264ef79f94e82d8bd6faa8'; dry_run = 'false' } 'a fresh stable tag'
        Assert-True -Condition ($fresh.Result.Output -clike '*Comparison mode: fresh*') -Name 'a pushed stable tag logs the fresh mode'

        $predates = Invoke-MetadataCommand @{ TAG = 'v0.9.3'; EVENT = 'push'; RUN_REF = 'refs/tags/v0.9.3' } $Token
        Assert-MetadataFails $predates 'predates the newest release' 'a stable tag older than the newest release'

        if ($descendsFromRelease) {
            Add-LocalTag "v0.9.2-test.$headShort" 'HEAD'
            Add-LocalTag 'v0.9.2-test.abcdef1' 'HEAD'
            $test = Invoke-MetadataCommand @{ TAG = "v0.9.2-test.$headShort"; EVENT = 'push'; RUN_REF = "refs/tags/v0.9.2-test.$headShort" } $Token
            Assert-MetadataPasses $test @{ version = '0.9.2'; kind = 'test'; notes_start = 'v0.9.1'; sha = $headSha; dry_run = 'false' } 'a test tag on HEAD'
            Assert-True -Condition ($test.Result.Output -clike '*Comparison mode: recovery*') -Name 'a test tag logs the recovery mode'

            $mismatch = Invoke-MetadataCommand @{ TAG = 'v0.9.2-test.abcdef1'; EVENT = 'push'; RUN_REF = 'refs/tags/v0.9.2-test.abcdef1' } $Token
            Assert-MetadataFails $mismatch 'Sha mismatch' 'a test tag naming another sha'
        }
        else {
            Write-Host 'skipping test tag checks: HEAD does not descend from v0.9.1'
        }
    }
    finally {
        Remove-LocalTags
    }
    foreach ($tag in $tags) {
        Assert-True -Condition ($null -eq (Get-TagCommit -Tag $tag)) -Name "local tag $tag is removed afterwards"
    }
}

function Test-MetadataIntegration {
    $token = Get-GitHubTokenForTests
    if (-not $token) {
        Write-Host 'skipping metadata integration: no GitHub token available'
        return
    }
    $unavailable = Test-MetadataPrerequisites
    if ($unavailable) {
        Write-Host "skipping metadata integration: $unavailable"
        return
    }

    $releases = @(Get-GitHubRelease -Repository 'codescene-oss/codescene-vs')
    Assert-True -Condition ($releases.Count -gt 0) -Name 'releases are loaded from GitHub'
    Assert-True -Condition (@($releases | Where-Object { $_.tag_name -ceq 'v0.9.1' }).Count -eq 1) -Name 'the loaded releases are flattened release objects'
    Assert-True -Condition (-not ($releases[0] -is [System.Array])) -Name 'the loaded releases are not nested pages'

    Test-MetadataDispatch $token
    Test-MetadataFreshTags $token
}
