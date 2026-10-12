$ErrorActionPreference = 'Stop'

$tag = $env:GITHUB_REF_NAME
$sha = $env:GITHUB_SHA
$repository = $env:GITHUB_REPOSITORY

if ([string]::IsNullOrWhiteSpace($tag)) {
    throw 'GITHUB_REF_NAME is not set.'
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$manifest = Join-Path $repoRoot 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.vsixmanifest'
$outputPath = Join-Path $repoRoot 'release-notes.md'

[xml]$xml = Get-Content $manifest
$vsixVersion = $xml.PackageManifest.Metadata.Identity.Version

$commitUrl = "https://github.com/$repository/commit/$sha"
$body = @"
Automated test build for ``$tag``.

- VSIX extension version: **$vsixVersion**
- Commit: [$sha]($commitUrl)

Install the ``.vsix`` from this release. The extension version shown in Visual Studio is **$vsixVersion**.
"@

Set-Content -Path $outputPath -Value $body -Encoding UTF8
Write-Host "Wrote $outputPath"
