function ConvertTo-MarketplaceOverview {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Readme
    )

    $baseUrl = 'https://raw.githubusercontent.com/codescene-oss/codescene-vs/main'
    $content = $Readme -replace 'src="screenshots/', "src=`"$baseUrl/screenshots/"
    return $content -replace '\]\(screenshots/', "]($baseUrl/screenshots/"
}

function Write-MarketplaceOverview {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $readme = $encoding.GetString([IO.File]::ReadAllBytes((Join-Path $RepositoryPath 'README.md')))
    $path = Join-Path $RepositoryPath 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\overview.md'
    [IO.File]::WriteAllBytes($path, $encoding.GetBytes((ConvertTo-MarketplaceOverview -Readme $readme)))
    return $path
}
