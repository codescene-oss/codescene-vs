$script:MaxVersionComponent = 65534
$script:TagPattern = '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-test\.([0-9a-f]{7,40}))?\z'
$script:VersionPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z'

function Test-VersionComponent([string]$Component) {
    return $Component.Length -le 5 -and [int]$Component -le $script:MaxVersionComponent
}

function Get-VersionComponents([string]$Version) {
    if ($Version -cnotmatch $script:VersionPattern) {
        throw "Malformed version '$Version'. Expected X.Y.Z with numeric components."
    }
    $components = @($Matches[1], $Matches[2], $Matches[3])
    foreach ($component in $components) {
        if (-not (Test-VersionComponent $component)) {
            throw "Malformed version '$Version'. Components must be at most $($script:MaxVersionComponent)."
        }
    }
    return @($components | ForEach-Object { [int]$_ })
}

function Get-ReleaseTagInfo {
    param(
        [Parameter(Mandatory)]
        [string]$Tag
    )

    $malformed = "Malformed tag '$Tag'. Expected vX.Y.Z (stable) or vX.Y.Z-test.<7-40 lowercase hex sha> (test), with each component at most $($script:MaxVersionComponent)."
    if ($Tag -cnotmatch $script:TagPattern) {
        throw $malformed
    }
    $parts = @($Matches[1], $Matches[2], $Matches[3])
    $sha = $null
    $kind = 'stable'
    if ($Matches[5]) {
        $kind = 'test'
        $sha = $Matches[5]
    }
    foreach ($part in $parts) {
        if (-not (Test-VersionComponent $part)) {
            throw $malformed
        }
    }

    return [pscustomobject]@{
        Tag     = $Tag
        Kind    = $kind
        Version = $parts -join '.'
        Sha     = $sha
    }
}

function Compare-ReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Left,
        [Parameter(Mandatory)]
        [string]$Right
    )

    $leftParts = Get-VersionComponents $Left
    $rightParts = Get-VersionComponents $Right
    for ($i = 0; $i -lt 3; $i++) {
        if ($leftParts[$i] -lt $rightParts[$i]) { return -1 }
        if ($leftParts[$i] -gt $rightParts[$i]) { return 1 }
    }
    return 0
}

function Get-NextReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$Bump
    )

    $parts = Get-VersionComponents $Version
    switch -CaseSensitive ($Bump) {
        'patch' { $next = @($parts[0], $parts[1], ($parts[2] + 1)) }
        'minor' { $next = @($parts[0], ($parts[1] + 1), 0) }
        'major' { $next = @(($parts[0] + 1), 0, 0) }
        default { throw "Invalid bump '$Bump'. Expected patch, minor or major." }
    }

    $result = $next -join '.'
    Get-VersionComponents $result | Out-Null
    return $result
}

function Get-TestReleaseTagName {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$ShortSha
    )

    $tag = "v$Version-test.$ShortSha"
    Get-ReleaseTagInfo -Tag $tag | Out-Null
    return $tag
}
