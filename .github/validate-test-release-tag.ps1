$ErrorActionPreference = 'Stop'

$tag = $env:GITHUB_REF_NAME
if ([string]::IsNullOrWhiteSpace($tag)) {
    throw 'GITHUB_REF_NAME is not set.'
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$manifest = Join-Path $repoRoot 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.vsixmanifest'

$m = [regex]::Match($tag, '^v(?<ver>\d+\.\d+\.\d+)-test\.(?<sha>[0-9a-f]+)$', 'IgnoreCase')
if (-not $m.Success) {
    throw "Tag '$tag' does not match vX.Y.Z-test.<sha>."
}

$tagSha = $m.Groups['sha'].Value
$tagVersion = $m.Groups['ver'].Value
$head = (git -C $repoRoot rev-parse HEAD).Trim()

if (-not $head.StartsWith($tagSha, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Tag SHA prefix '$tagSha' does not match commit $head."
}

if (-not (Test-Path $manifest)) {
    throw "VSIX manifest not found: $manifest"
}

[xml]$xml = Get-Content $manifest
$manifestVersion = $xml.PackageManifest.Metadata.Identity.Version
if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw 'Could not read Identity/@Version from VSIX manifest.'
}

if ($manifestVersion -ne $tagVersion) {
    throw "VSIX manifest version '$manifestVersion' does not match tag version '$tagVersion'."
}

Write-Host "Test release tag validated: $tag (VSIX $manifestVersion, commit $head)"
