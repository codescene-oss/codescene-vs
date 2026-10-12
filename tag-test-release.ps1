param(
    [int]$ShaLength = 7,
    [switch]$WhatIf,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'

$manifest = Join-Path $PSScriptRoot 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.vsixmanifest'

function Get-VersionFromManifest {
    param($path)
    if (-not (Test-Path $path)) { throw "VSIX manifest not found: $path" }
    [xml]$xml = Get-Content $path
    $ver = $xml.PackageManifest.Metadata.Identity.Version
    if ([string]::IsNullOrWhiteSpace($ver)) { throw 'Could not read Identity/@Version.' }
    return $ver
}

Push-Location $PSScriptRoot
try {
    $version = Get-VersionFromManifest $manifest
    $shortSha = (git rev-parse --short=$ShaLength HEAD).Trim()
    $tagName = "v$version-test.$shortSha"

    if ($WhatIf) {
        Write-Host "Would create tag: $tagName (VSIX version $version)"
        return
    }

    $localTag = git tag -l $tagName
    if ($localTag) {
        throw "Tag '$tagName' already exists locally."
    }

    git ls-remote --exit-code --tags origin "refs/tags/$tagName" 2>$null
    if ($LASTEXITCODE -eq 0) {
        throw "Tag '$tagName' already exists on origin."
    }
    if ($LASTEXITCODE -ne 2) {
        throw "Failed to check remote for tag '$tagName' (exit code $LASTEXITCODE)."
    }

    git tag -a $tagName -m "Test release $version ($shortSha)"
    Write-Host "Created tag $tagName"

    if (-not $NoPush) {
        git push origin $tagName
        Write-Host "Pushed $tagName to origin."
    } else {
        Write-Host "Skipped push (-NoPush)."
    }
} finally {
    Pop-Location
}
