function Get-ReleaseTagName($Release) {
    if ($Release -is [string]) { return $Release }
    return $Release.tag_name
}

function Select-PublishedStableRelease {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Releases,
        [AllowNull()][AllowEmptyString()]
        [string]$ExcludeTag
    )

    foreach ($release in @($Releases | Where-Object { $null -ne $_ })) {
        if ($release.draft -or $release.prerelease) { continue }
        if ($ExcludeTag -and $release.tag_name -ceq $ExcludeTag) { continue }
        try {
            $info = Get-ReleaseTagInfo -Tag $release.tag_name
        }
        catch {
            continue
        }
        if ($info.Kind -ceq 'stable') { $release }
    }
}

function Sort-ReleaseByVersionDescending {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Releases
    )

    $keyed = foreach ($release in @($Releases)) {
        $parts = Get-VersionComponents (Get-ReleaseTagInfo -Tag $release.tag_name).Version
        [pscustomobject]@{ Major = $parts[0]; Minor = $parts[1]; Patch = $parts[2]; Release = $release }
    }
    @($keyed) | Sort-Object -Property Major, Minor, Patch -Descending | ForEach-Object { $_.Release }
}

function Select-HighestStableRelease {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Releases
    )

    $published = @(Select-PublishedStableRelease -Releases $Releases)
    if ($published.Count -eq 0) { return $null }
    return @(Sort-ReleaseByVersionDescending -Releases $published)[0]
}

function Select-PreviousStableRelease {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Releases,
        [Parameter(Mandatory)]
        [scriptblock]$IsAncestor
    )

    $published = @(Select-PublishedStableRelease -Releases $Releases)
    foreach ($release in @(Sort-ReleaseByVersionDescending -Releases $published)) {
        if (& $IsAncestor $release.tag_name) { return $release }
    }
    return $null
}
