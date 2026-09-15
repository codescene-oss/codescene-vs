Set-StrictMode -Version Latest

$projectRelativePath = "Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022"
$manifestFileName = "source.extension.vsixmanifest"
$sourceFileName = "source.extension.cs"

function Assert-Version {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [ValidateSet("stable", "package")]
        [string]$Kind
    )

    $formats = @{
        stable = @{
            Pattern = "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$"
            Description = "version in X.Y.Z format"
        }
        package = @{
            Pattern = "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(\.(0|[1-9]\d*))?$"
            Description = "numeric VSIX version in X.Y.Z or X.Y.Z.N format"
        }
    }

    $format = $formats[$Kind]
    if ($Version -notmatch $format.Pattern) {
        throw "Expected $($format.Description), got '$Version'."
    }
    if ($Version.Split(".") | Where-Object { [int64]$_ -gt 65534 }) {
        throw "Version components must not exceed 65534, got '$Version'."
    }
}

function Get-PackagePaths {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath
    )

    $projectPath = Join-Path $RepositoryPath $projectRelativePath
    return [pscustomobject]@{
        Manifest = Join-Path $projectPath $manifestFileName
        Source = Join-Path $projectPath $sourceFileName
    }
}

function Get-NextStableVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$Bump
    )

    Assert-Version -Version $Version -Kind "stable"

    $parts = [int64[]]$Version.Split(".")
    switch ($Bump) {
        "patch" {
            $parts[2]++
        }
        "minor" {
            $parts[1]++
            $parts[2] = 0
        }
        "major" {
            $parts[0]++
            $parts[1] = 0
            $parts[2] = 0
        }
        default {
            throw "Expected patch, minor, or major, got '$Bump'."
        }
    }

    $nextVersion = $parts -join "."
    Assert-Version -Version $nextVersion -Kind "stable"
    return $nextVersion
}

function Get-TestReleaseMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$StableVersion,
        [Parameter(Mandatory)]
        [string]$Bump,
        [Parameter(Mandatory)]
        [ValidateRange(0, 65534)]
        [int]$Revision
    )

    $baseVersion = Get-NextStableVersion -Version $StableVersion -Bump $Bump
    return [pscustomobject]@{
        BaseVersion = $baseVersion
        PackageVersion = "$baseVersion.$Revision"
        Tag = "v$baseVersion-test.$Revision"
    }
}

function Get-PackageVersions {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath
    )

    $paths = Get-PackagePaths -RepositoryPath $RepositoryPath
    if (-not (Test-Path $paths.Manifest)) {
        throw "VSIX manifest not found: $($paths.Manifest)"
    }
    if (-not (Test-Path $paths.Source)) {
        throw "VSIX source metadata not found: $($paths.Source)"
    }

    [xml]$manifest = Get-Content $paths.Manifest -Raw
    $manifestVersion = [string]$manifest.PackageManifest.Metadata.Identity.Version
    $source = Get-Content $paths.Source -Raw
    $sourceMatch = [regex]::Match($source, 'public const string Version = "([^"]+)";')
    if (-not $sourceMatch.Success) {
        throw "Could not read Vsix.Version from '$($paths.Source)'."
    }

    return [pscustomobject]@{
        ManifestVersion = $manifestVersion
        SourceVersion = $sourceMatch.Groups[1].Value
    }
}

function Set-PackageVersion {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Version
    )

    Assert-Version -Version $Version -Kind "package"
    $paths = Get-PackagePaths -RepositoryPath $RepositoryPath
    $versions = Get-PackageVersions -RepositoryPath $RepositoryPath

    [xml]$manifest = Get-Content $paths.Manifest -Raw
    $manifest.PackageManifest.Metadata.Identity.Version = $Version
    $manifest.Save($paths.Manifest)

    $source = Get-Content $paths.Source -Raw
    $currentDeclaration = "public const string Version = `"$($versions.SourceVersion)`";"
    $updatedDeclaration = "public const string Version = `"$Version`";"
    $updatedSource = $source.Replace($currentDeclaration, $updatedDeclaration)
    if ($updatedSource -eq $source) {
        throw "Could not update Vsix.Version in '$($paths.Source)'."
    }

    [System.IO.File]::WriteAllText($paths.Source, $updatedSource, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-GitCommand {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string[]]$GitArguments
    )

    $output = & git -C $RepositoryPath @GitArguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function New-TestReleaseTag {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [ValidateSet("patch", "minor", "major")]
        [string]$Bump = "patch"
    )

    $versions = Get-PackageVersions -RepositoryPath $RepositoryPath
    if ($versions.ManifestVersion -ne $versions.SourceVersion) {
        throw "Manifest version '$($versions.ManifestVersion)' and source version '$($versions.SourceVersion)' must match."
    }

    Assert-Version -Version $versions.ManifestVersion -Kind "stable"
    $status = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("status", "--porcelain")
    if ($status) {
        throw "Test releases require a clean Git worktree."
    }

    $revision = [int](Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("rev-list", "--count", "HEAD"))
    $metadata = Get-TestReleaseMetadata -StableVersion $versions.ManifestVersion -Bump $Bump -Revision $revision
    $existingTag = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("tag", "--list", $metadata.Tag)
    if ($existingTag) {
        throw "Tag already exists: $($metadata.Tag)"
    }

    Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @(
        "tag",
        "-a",
        $metadata.Tag,
        "-m",
        "Test release $($metadata.Tag)"
    ) | Out-Null

    return $metadata
}

function Get-ReachableTags {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Commit
    )

    $output = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("tag", "--merged", $Commit)
    if (-not $output) {
        return @()
    }

    return @($output -split "\r?\n")
}

function Get-StableTags {
    param(
        [Parameter(Mandatory)]
        [string[]]$Tags,
        [string]$Exclude
    )

    $stableTags = foreach ($candidate in $Tags) {
        if ($candidate -eq $Exclude) {
            continue
        }
        if ($candidate -match "^v(?<version>(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*))$") {
            [pscustomobject]@{
                Tag = $candidate
                Version = [version]$Matches.version
            }
        }
    }

    return @($stableTags | Sort-Object Version -Descending)
}

function ConvertTo-TestTag {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Tag,
        [Parameter(Mandatory)]
        [string]$Pattern
    )

    $match = [regex]::Match($Tag, $Pattern)
    if (-not $match.Success) {
        return
    }

    $revision = [int]$match.Groups["revision"].Value
    $commit = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("rev-parse", "--verify", "$Tag`^{commit}")
    $commitCount = [int](Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("rev-list", "--count", $commit))
    if ($revision -ne $commitCount) {
        return
    }

    return [pscustomobject]@{
        Tag = $Tag
        Revision = $revision
    }
}

function Get-TestTags {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string[]]$Tags,
        [Parameter(Mandatory)]
        [string]$BaseVersion,
        [string]$Exclude
    )

    $escapedBaseVersion = [regex]::Escape($BaseVersion)
    $pattern = "^v$escapedBaseVersion-test\.(?<revision>\d+)$"
    $candidates = $Tags | Where-Object { $_ -ne $Exclude }
    $testTags = $candidates | ForEach-Object {
        ConvertTo-TestTag -RepositoryPath $RepositoryPath -Tag $_ -Pattern $pattern
    }
    return @($testTags | Sort-Object Revision -Descending)
}

function Get-StableReleaseMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$Tag,
        [Parameter(Mandatory)]
        [string]$TagVersion,
        [Parameter(Mandatory)]
        [string]$CommittedVersion,
        [Parameter(Mandatory)]
        [string[]]$ReachableTags
    )

    if ($TagVersion -ne $CommittedVersion) {
        throw "Stable tag version '$TagVersion' must match committed version '$CommittedVersion'."
    }

    $previousStableTags = @(Get-StableTags -Tags $ReachableTags -Exclude $Tag)
    $previousStableTag = if ($previousStableTags.Count -gt 0) { $previousStableTags[0].Tag } else { "" }
    return [pscustomobject]@{
        IsRelease = $true
        IsTest = $false
        Tag = $Tag
        PackageVersion = $TagVersion
        NotesStartTag = $previousStableTag
        LatestStableTag = $previousStableTag
    }
}

function Get-TestTagReleaseMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Tag,
        [Parameter(Mandatory)]
        [System.Text.RegularExpressions.Match]$TagMatch,
        [Parameter(Mandatory)]
        [string]$CommittedVersion,
        [Parameter(Mandatory)]
        [string]$Commit,
        [Parameter(Mandatory)]
        [string[]]$ReachableTags
    )

    $baseVersion = $TagMatch.Groups["base"].Value
    $revision = [int]$TagMatch.Groups["revision"].Value
    $validBaseVersions = @(
        Get-NextStableVersion -Version $CommittedVersion -Bump "patch"
        Get-NextStableVersion -Version $CommittedVersion -Bump "minor"
        Get-NextStableVersion -Version $CommittedVersion -Bump "major"
    )
    if ($baseVersion -notin $validBaseVersions) {
        throw "Test base version '$baseVersion' must be the next patch, minor, or major version after '$CommittedVersion'."
    }

    $commitCount = [int](Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("rev-list", "--count", $Commit))
    if ($revision -ne $commitCount) {
        throw "Test tag revision '$revision' must match tagged commit count '$commitCount'."
    }

    $stableTags = @(Get-StableTags -Tags $ReachableTags)
    $latestStableTag = if ($stableTags.Count -gt 0) { $stableTags[0].Tag } else { "" }
    $previousTestTags = @(
        Get-TestTags -RepositoryPath $RepositoryPath -Tags $ReachableTags -BaseVersion $baseVersion -Exclude $Tag
    )
    $notesStartTag = if ($previousTestTags.Count -gt 0) { $previousTestTags[0].Tag } else { $latestStableTag }

    return [pscustomobject]@{
        IsRelease = $true
        IsTest = $true
        Tag = $Tag
        PackageVersion = "$baseVersion.$revision"
        NotesStartTag = $notesStartTag
        LatestStableTag = $latestStableTag
    }
}

function Get-ReleaseMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Tag
    )

    $versions = Get-PackageVersions -RepositoryPath $RepositoryPath
    if ($versions.ManifestVersion -ne $versions.SourceVersion) {
        throw "Manifest version '$($versions.ManifestVersion)' and source version '$($versions.SourceVersion)' must match."
    }
    Assert-Version -Version $versions.ManifestVersion -Kind "stable"

    $stableMatch = [regex]::Match($Tag, "^v(?<version>(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*))$")
    $testMatch = [regex]::Match($Tag, "^v(?<base>(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*))-test\.(?<revision>\d+)$")
    if (-not $stableMatch.Success -and -not $testMatch.Success) {
        throw "Expected release tag vX.Y.Z or vX.Y.Z-test.N, got '$Tag'."
    }

    $tagType = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("cat-file", "-t", $Tag)
    if ($tagType -ne "tag") {
        throw "Release tag '$Tag' must be annotated."
    }

    $commit = Invoke-GitCommand -RepositoryPath $RepositoryPath -GitArguments @("rev-parse", "--verify", "$Tag`^{commit}")
    $reachableTags = @(Get-ReachableTags -RepositoryPath $RepositoryPath -Commit $commit)
    if ($stableMatch.Success) {
        return Get-StableReleaseMetadata `
            -Tag $Tag `
            -TagVersion $stableMatch.Groups["version"].Value `
            -CommittedVersion $versions.ManifestVersion `
            -ReachableTags $reachableTags
    }

    return Get-TestTagReleaseMetadata `
        -RepositoryPath $RepositoryPath `
        -Tag $Tag `
        -TagMatch $testMatch `
        -CommittedVersion $versions.ManifestVersion `
        -Commit $commit `
        -ReachableTags $reachableTags
}

Export-ModuleMember -Function Get-NextStableVersion, Get-TestReleaseMetadata, Get-PackageVersions, Set-PackageVersion, New-TestReleaseTag, Get-ReleaseMetadata
