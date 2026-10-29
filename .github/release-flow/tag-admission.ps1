function New-TagAdmissionFacts {
    param(
        [Parameter(Mandatory)]
        [bool]$OnMain,
        [AllowNull()]
        $Highest,
        [AllowNull()]
        $HighestIsAncestor,
        [AllowNull()]
        $Previous,
        [AllowNull()][AllowEmptyString()]
        [string]$TaggedCommit
    )

    return [pscustomobject]@{
        OnMain            = $OnMain
        Highest           = $Highest
        HighestIsAncestor = $HighestIsAncestor
        Previous          = $Previous
        TaggedCommit      = $TaggedCommit
    }
}

function Assert-TagOnMain($TagInfo, [bool]$OnMain) {
    if ($TagInfo.Kind -ceq 'stable' -and -not $OnMain) {
        throw "Tag $($TagInfo.Tag) is not on main. Stable releases must be tagged on a commit reachable from main."
    }
}

function Assert-VersionExceedsRelease($TagInfo, $Release, [string]$Shipped) {
    $releaseTag = Get-ReleaseTagName $Release
    $releaseVersion = (Get-ReleaseTagInfo -Tag $releaseTag).Version
    if ((Compare-ReleaseVersion -Left $TagInfo.Version -Right $releaseVersion) -le 0) {
        throw "Version $($TagInfo.Version) of tag $($TagInfo.Tag) does not exceed $Shipped ($releaseTag)."
    }
}

function Assert-FreshTagAdmissible($TagInfo, $Facts) {
    if ($null -eq $Facts.Highest) { return }
    Assert-VersionExceedsRelease $TagInfo $Facts.Highest 'the release already shipped'
    if ($null -ne $Facts.HighestIsAncestor -and -not $Facts.HighestIsAncestor) {
        $highestTag = Get-ReleaseTagName $Facts.Highest
        throw "Tag $($TagInfo.Tag) predates the newest release ${highestTag}: that release is not an ancestor of the tagged commit."
    }
}

function Assert-RecoveryTagAdmissible($TagInfo, $Facts) {
    if ($null -eq $Facts.Previous) { return }
    Assert-VersionExceedsRelease $TagInfo $Facts.Previous 'the release already shipped before it'
}

function Assert-TestShaMatchesCommit($TagInfo, [string]$TaggedCommit) {
    if ($TagInfo.Kind -cne 'test') { return }
    if (-not $TaggedCommit -or -not $TaggedCommit.StartsWith($TagInfo.Sha, [StringComparison]::Ordinal)) {
        throw "Sha mismatch: tag $($TagInfo.Tag) names sha $($TagInfo.Sha) but the tagged commit is '$TaggedCommit'."
    }
}

function Assert-TagAdmissible {
    param(
        [Parameter(Mandatory)]
        $TagInfo,
        [Parameter(Mandatory)]
        [ValidateSet('fresh', 'recovery')]
        [string]$Mode,
        [Parameter(Mandatory)]
        $Facts
    )

    Assert-TagOnMain $TagInfo $Facts.OnMain
    if ($Mode -ceq 'fresh') { Assert-FreshTagAdmissible $TagInfo $Facts }
    if ($Mode -ceq 'recovery') { Assert-RecoveryTagAdmissible $TagInfo $Facts }
    Assert-TestShaMatchesCommit $TagInfo $Facts.TaggedCommit
}
