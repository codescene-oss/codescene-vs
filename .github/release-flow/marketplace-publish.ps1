function Test-PublisherVersionExists {
    param(
        [AllowNull()][AllowEmptyString()]
        [string[]]$Output
    )

    $text = @($Output | Where-Object { $null -ne $_ }) -join "`n"
    return [bool]($text -match '(?i)already exists|version\b.*\bexists')
}

function Find-SingleVsix {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $found = @(Get-ChildItem -LiteralPath $Root -Recurse -File | Where-Object { $_.Name -like '*.vsix' })
    if ($found.Count -eq 0) {
        throw "No .vsix file found under $Root."
    }
    if ($found.Count -gt 1) {
        throw "Expected exactly one .vsix under $Root but found $($found.Count): $(($found | ForEach-Object { $_.FullName }) -join ', ')."
    }
    return $found[0].FullName
}

function Invoke-MarketplacePublish {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$PublisherPath,
        [Parameter(Mandatory)]
        [string]$VsixDirectory,
        [Parameter(Mandatory)]
        [string]$PublishManifest,
        [scriptblock]$Publisher = { param($FilePath, $ArgumentList) Invoke-Native -FilePath $FilePath -ArgumentList $ArgumentList },
        [scriptblock]$IsListed = { param($Version) Test-MarketplaceVersionListed -Version $Version }
    )

    if (-not (Test-Path -LiteralPath $PublishManifest -PathType Leaf)) {
        throw "publishManifest.json not found at $PublishManifest."
    }
    $vsix = Find-SingleVsix -Root $VsixDirectory
    if (& $IsListed $Version) {
        Write-Host "Version $Version is already listed on the Marketplace; skipping publish"
        return 'listed'
    }
    Write-Host "Publishing $vsix with manifest $PublishManifest"
    $result = & $Publisher $PublisherPath @('publish', '-payload', $vsix, '-publishManifest', $PublishManifest)
    if ($result.Output) { Write-Host $result.Output }
    if ($result.ExitCode -eq 0) {
        Write-Host "Published version $Version to the Marketplace"
        return 'published'
    }
    if (Test-PublisherVersionExists -Output $result.Output) {
        Write-Host "Version $Version already exists on the Marketplace; skipping publish"
        return 'exists'
    }
    throw "Publishing $vsix failed with exit code $($result.ExitCode): $($result.Output)"
}
