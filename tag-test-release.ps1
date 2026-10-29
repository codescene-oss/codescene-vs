param(
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump = 'patch',
    [int]$ShaLength = 7,
    [switch]$WhatIf,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'

$repository = 'codescene-oss/codescene-vs'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required to find the latest release. Install it from https://cli.github.com/.'
}

Import-Module (Join-Path $PSScriptRoot '.github\release-flow.psm1') -Force

if ((Invoke-Native -FilePath 'gh' -ArgumentList @('auth', 'status')).ExitCode -ne 0) {
    throw 'gh is not authenticated. Run "gh auth login" first.'
}

Push-Location $PSScriptRoot
try {
    $dirty = (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('status', '--porcelain')).Output
    if ($dirty) {
        throw "Tagging a test release requires a clean worktree. Commit or stash these changes first:`n$dirty"
    }

    $releases = @(Get-GitHubRelease -Repository $repository)
    $highest = Select-HighestStableRelease -Releases $releases
    if (-not $highest) {
        throw "No published stable release found in $repository."
    }

    $highestInfo = Get-ReleaseTagInfo -Tag $highest.tag_name
    $nextVersion = Get-NextReleaseVersion -Version $highestInfo.Version -Bump $Bump
    $shortSha = (git rev-parse --short=$ShaLength HEAD).Trim()
    $tagName = Get-TestReleaseTagName -Version $nextVersion -ShortSha $shortSha

    $localTag = git tag -l $tagName
    if ($localTag) {
        throw "Tag '$tagName' already exists locally."
    }

    $remoteCheck = Invoke-Native -FilePath 'git' -ArgumentList @('ls-remote', '--exit-code', '--tags', 'origin', "refs/tags/$tagName")
    if ($remoteCheck.ExitCode -eq 0) {
        throw "Tag '$tagName' already exists on origin."
    }
    if ($remoteCheck.ExitCode -ne 2) {
        throw "Failed to check remote for tag '$tagName' (exit code $($remoteCheck.ExitCode)): $($remoteCheck.Output)"
    }

    if ($WhatIf) {
        Write-Host "Would create tag: $tagName (next version $nextVersion after $($highest.tag_name))"
        return
    }

    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('tag', '-a', $tagName, '-m', "Test release $nextVersion ($shortSha)") | Out-Null
    Write-Host "Created tag $tagName"

    if (-not $NoPush) {
        Invoke-NativeChecked -FilePath 'git' -ArgumentList @('push', 'origin', $tagName) | Out-Null
        Write-Host "Pushed $tagName to origin."
    } else {
        Write-Host "Skipped push (-NoPush)."
    }
} finally {
    Pop-Location
}
