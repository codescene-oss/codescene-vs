# --- SETTINGS (adjust paths if needed) ---
$manifest  = 'Codescene.VSExtension.VS2022/source.extension.vsixmanifest'
$changelog = Join-Path $PSScriptRoot "CHANGELOG.md"

# --- 1) Determine current version from CHANGELOG (preferred), fallback to manifest ---
function Get-VersionFromChangelog {
    param($path)
    if (-not (Test-Path $path)) { return $null }
    $content = Get-Content $path -Raw
    # First numeric version after [Unreleased]
    $m = [regex]::Match($content, '## \[(\d+\.\d+\.\d+)\]')
    if ($m.Success) { return $m.Groups[1].Value } else { return $null }
}

function Get-VersionFromManifest {
    param($path)
    if (-not (Test-Path $path)) { throw "VSIX manifest not found: $path" }
    [xml]$xml = Get-Content $path
    $ver = $xml.PackageManifest.Metadata.Identity.Version
    if ([string]::IsNullOrWhiteSpace($ver)) { throw "Could not read Identity/@Version from VSIX manifest." }
    return $ver
}

$currentVersion = Get-VersionFromChangelog $changelog
if (-not $currentVersion) { $currentVersion = Get-VersionFromManifest $manifest }

Write-Host "Current version: $currentVersion"

$parts = $currentVersion.Split('.')
if ($parts.Count -lt 3) { throw "Expected semver (X.Y.Z), got '$currentVersion'." }
[int]$major = $parts[0]; [int]$minor = $parts[1]; [int]$patch = $parts[2]

# --- 2) Ask for release type ---
$releaseType = Read-Host "Release type (patch / minor / major)"
switch ($releaseType) {
    'major' { $major++; $minor=0; $patch=0 }
    'minor' { $minor++; $patch=0 }
    'patch' { $patch++ }
  default { throw "Invalid release type '$releaseType'. Use patch/minor/major." }
}
$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion"

# --- 3) Collect commits since last tag and group them ---
try { $lastTag = (git describe --tags --abbrev=0).Trim() } catch { $lastTag = "" }
if ([string]::IsNullOrWhiteSpace($lastTag)) {
    $rawCommits = git log --pretty=format:"%s"
} else { 
    $rawCommits = git log "$lastTag..HEAD" --pretty=format:"%s"
}

$added   = New-Object System.Collections.Generic.List[string]
$fixed   = New-Object System.Collections.Generic.List[string]
$changed = New-Object System.Collections.Generic.List[string]

foreach ($c in $rawCommits) {
    if ([string]::IsNullOrWhiteSpace($c)) { continue }
    if ($c -like 'Merge*') { continue } # skip merge noise
    if ($c -match '^feat(\(.+?\))?:\s*(.+)$')      { $added.Add("- " + $Matches[2].Trim())  ; continue }
    if ($c -match '^fix(\(.+?\))?:\s*(.+)$')       { $fixed.Add("- " + $Matches[2].Trim())  ; continue }
    if ($c -match '^(docs|chore|refactor|style|perf|build|ci|test)(\(.+?\))?:\s*(.+)$') {
        $changed.Add("- " + $Matches[3].Trim()); continue
    }
    # default bucket
    $changed.Add("- " + $c.Trim())
}

# --- 4) Build new changelog section ---
$today = Get-Date -Format 'yyyy-MM-dd'
$newSection = "## [$newVersion] - $today`r`n"
if ($added.Count   -gt 0) { $newSection += "### Added`r`n"   + ($added   -join "`r`n") + "`r`n" }
if ($fixed.Count   -gt 0) { $newSection += "### Fixed`r`n"   + ($fixed   -join "`r`n") + "`r`n" }
if ($changed.Count -gt 0) { $newSection += "### Changed`r`n" + ($changed -join "`r`n") + "`r`n" }

# --- 5) Update CHANGELOG.md (keep [Unreleased] at the top) ---
if (-not (Test-Path $changelog)) {
    $content = "## [Unreleased]`r`n`r`n$newSection"
    Set-Content -Path $changelog -Value $content -Encoding UTF8
} else {
    $old = Get-Content $changelog -Raw
    if ($old -notmatch '## \[Unreleased\]') { $old = "## [Unreleased]`r`n`r`n" + $old }
    $updated = [regex]::Replace($old, '(## \[Unreleased\])', "`$1`r`n`r`n$newSection", 1)
    Set-Content -Path $changelog -Value $updated -Encoding UTF8
    }

# --- 6) Update ONLY Identity/@Version in the VSIX manifest via XML ---
[xml]$mxml = Get-Content $manifest
$identity = $mxml.PackageManifest.Metadata.Identity
if (-not $identity) { throw "Could not find /PackageManifest/Metadata/Identity in manifest." }
$identity.Version = $newVersion

# Save manifest as UTF-8 without BOM
$utf8 = New-Object System.Text.UTF8Encoding($false)
$sw = New-Object System.IO.StreamWriter($manifest, $false, $utf8)
$mxml.Save($sw); $sw.Dispose()

# --- 7) Commit and Tag (no literal $newVersion issues) ---
git add -- $manifest $changelog | Out-Null
$commitMsg = "chore(release): v$newVersion"
git commit -m $commitMsg
git tag "v$newVersion"

Write-Host ""
Write-Host "✔ Release v$newVersion created."
Write-Host "   Push with: git push origin --follow-tags"
