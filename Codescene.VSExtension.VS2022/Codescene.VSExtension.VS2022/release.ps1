# === PARAMETERS ===
$manifest = 'Codescene.VSExtension.VS2022/source.extension.vsixmanifest'
$csproj = 'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022.csproj'
$changelog = 'CHANGELOG.md'

# === 1. Determine next version ===
$currentVersion = (Select-String 'Version="([0-9\.]+)"' $manifest).Matches.Groups[1].Value
Write-Host "Current version: $currentVersion"
$parts = $currentVersion.Split('.')
$major, $minor, $patch = [int]$parts[0], [int]$parts[1], [int]$parts[2]

$releaseType = Read-Host "Release type (patch / minor / major)"
switch ($releaseType) {
    'major' { $major++; $minor=0; $patch=0 }
    'minor' { $minor++; $patch=0 }
    'patch' { $patch++ }
    default { Write-Error "Invalid release type"; exit 1 }
}
$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion"

# === 2. Collect commits since last tag ===
try { $lastTag = git describe --tags --abbrev=0 } catch { $lastTag = "" }
if ($lastTag -eq "") { 
    $commits = git log --pretty=format:"%s" 
} else { 
    $commits = git log $lastTag..HEAD --pretty=format:"%s" 
}

$added = @(); $fixed = @(); $changed = @()
foreach ($c in $commits) {
    if ($c -match '^feat:') { $added += "- " + $c.Substring(5).Trim() }
    elseif ($c -match '^fix:') { $fixed += "- " + $c.Substring(4).Trim() }
    else { $changed += "- " + $c.Trim() }
}

# === 3. Prepare new changelog section ===
$date = Get-Date -Format "yyyy-MM-dd"
$newSection = "## [$newVersion] - $date`n"
if ($added.Count -gt 0) { $newSection += "### Added`n" + ($added -join "`n") + "`n" }
if ($fixed.Count -gt 0) { $newSection += "### Fixed`n" + ($fixed -join "`n") + "`n" }
if ($changed.Count -gt 0) { $newSection += "### Changed`n" + ($changed -join "`n") + "`n" }

# === 4. Update CHANGELOG.md ===
if (-Not (Test-Path $changelog)) { 
    # If no changelog exists, create with Unreleased section
    Set-Content $changelog "## [Unreleased]`n`n$newSection"
} else {
    $oldContent = Get-Content $changelog -Raw

    # Ensure [Unreleased] section exists at the top
    if ($oldContent -notmatch '## \[Unreleased\]') {
        $oldContent = "## [Unreleased]`n`n" + $oldContent
    }

    # Insert new version section **after [Unreleased]**
    $updatedContent = $oldContent -replace '(## \[Unreleased\])', "`$1`n`n$newSection"
    Set-Content $changelog $updatedContent
}

# === 5. Update VSIX manifest & csproj ===
(Get-Content $manifest) -replace 'Version="[0-9\.]+"', ('Version="' + $newVersion + '"') | Set-Content $manifest
(Get-Content $csproj) -replace '<Version>[0-9\.]+</Version>', ('<Version>' + $newVersion + '</Version>') | Set-Content $csproj

# === 6. Commit and tag ===
git add $manifest $csproj $changelog
git commit -m "chore(release): $newVersion"
git tag "v$newVersion"

Write-Host "✔ Release $newVersion created. Push with: git push origin main --follow-tags"
Write-Host "The [Unreleased] section remains at the top for the next release."
