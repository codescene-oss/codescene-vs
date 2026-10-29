function Expand-JsonPage($Item) {
    if ($Item -is [System.Array]) {
        foreach ($child in $Item) { Expand-JsonPage $child }
        return
    }
    if ($null -ne $Item) { $Item }
}

function Get-GitHubRelease {
    param(
        [Parameter(Mandatory)]
        [string]$Repository
    )

    $result = Invoke-NativeChecked -FilePath 'gh' -ArgumentList @('api', '--paginate', '--slurp', "repos/$Repository/releases?per_page=100")
    $pages = $result.Output | ConvertFrom-Json
    return @(Expand-JsonPage $pages)
}
