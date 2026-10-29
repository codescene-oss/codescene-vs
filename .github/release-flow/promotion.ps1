function Get-PromotionAction {
    param(
        [Parameter(Mandatory)]
        [string]$Tag,
        [AllowNull()][AllowEmptyString()]
        [string]$LatestTag
    )

    $info = Get-ReleaseTagInfo -Tag $Tag
    if ([string]::IsNullOrEmpty($LatestTag)) { return 'promote' }
    if ($LatestTag -ceq $Tag) { return 'unchanged' }

    $latest = Get-ReleaseTagInfo -Tag $LatestTag
    if ($latest.Kind -cne 'stable') {
        throw "The latest release $LatestTag is not a stable release tag."
    }
    $comparison = Compare-ReleaseVersion -Left $info.Version -Right $latest.Version
    if ($comparison -gt 0) { return 'promote' }
    if ($comparison -lt 0) { return 'superseded' }
    return 'unchanged'
}

function Get-LatestReleaseTag {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [scriptblock]$Invoker = { param($FilePath, $ArgumentList) Invoke-Native -FilePath $FilePath -ArgumentList $ArgumentList }
    )

    $result = & $Invoker 'gh' @('api', "repos/$Repository/releases/latest")
    if ($result.ExitCode -ne 0) {
        if ($result.Output -match 'HTTP 404') { return $null }
        throw "gh api repos/$Repository/releases/latest failed with exit code $($result.ExitCode): $($result.Output)"
    }
    $tag = Get-OptionalProperty ($result.Output | ConvertFrom-Json) 'tag_name'
    if ([string]::IsNullOrEmpty($tag)) {
        throw "The latest release of $Repository has no tag name."
    }
    return [string]$tag
}
