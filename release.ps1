# --- SETTINGS ---
$manifest  = 'Codescene.VSExtension.VS2022/source.extension.vsixmanifest'
$vsix      = 'Codescene.VSExtension.VS2022/source.extension.cs'
$changelog = Join-Path $PSScriptRoot "CHANGELOG.md"

function Get-VersionFromChangelog {
    param($path)
    if (-not (Test-Path $path)) { return $null }
    $content = Get-Content $path -Raw
    $m = [regex]::Match($content, '## \[(\d+\.\d+\.\d+)\]')
    if ($m.Success) { return $m.Groups[1].Value } else { return $null }
}

function Get-VersionFromManifest {
    param($path)
    if (-not (Test-Path $path)) { throw "VSIX manifest not found: $path" }
    [xml]$xml = Get-Content $path
    $ver = $xml.PackageManifest.Metadata.Identity.Version
    if ([string]::IsNullOrWhiteSpace($ver)) { throw "Could not read Identity/@Version." }
    return $ver
}

function Bump-Version {
    param($version)
    $parts = $version.Split('.')
    if ($parts.Count -lt 3) { throw "Expected semver (X.Y.Z), got '$version'." }
    [int]$major = $parts[0]; [int]$minor = $parts[1]; [int]$patch = $parts[2]

    $releaseType = Read-Host "Release type (patch / minor / major)"
    switch ($releaseType) {
        'major' { $major++; $minor=0; $patch=0 }
        'minor' { $minor++; $patch=0 }
        'patch' { $patch++ }
        default { throw "Invalid release type '$releaseType'. Use patch/minor/major." }
    }
    return "$major.$minor.$patch"
}

function Collect-Commits {
    try { $lastTag = (git describe --tags --abbrev=0).Trim() } catch { $lastTag = "" }
    if ([string]::IsNullOrWhiteSpace($lastTag)) {
        return git log --pretty=format:"%s"
    } else { 
        return git log "$lastTag..HEAD" --pretty=format:"%s"
    }
}

function Group-Commits {
    param($rawCommits)
    $result = @{
        Added   = New-Object System.Collections.Generic.List[string]
        Fixed   = New-Object System.Collections.Generic.List[string]
        Changed = New-Object System.Collections.Generic.List[string]
    }

    foreach ($c in $rawCommits) {
        if ([string]::IsNullOrWhiteSpace($c)) { continue }
        if ($c -like 'Merge*') { continue }

        if ($c -match '^feat(\(.+?\))?:\s*(.+)$')      { $result.Added.Add("- " + $Matches[2].Trim()); continue }
        if ($c -match '^fix(\(.+?\))?:\s*(.+)$')       { $result.Fixed.Add("- " + $Matches[2].Trim()); continue }
        if ($c -match '^(docs|chore|refactor|style|perf|build|ci|test)(\(.+?\))?:\s*(.+)$') {
            $result.Changed.Add("- " + $Matches[3].Trim()); continue
        }
        $result.Changed.Add("- " + $c.Trim())
    }
    return $result
}

function Update-Changelog {
    param($version, $groups)

    $today = Get-Date -Format 'yyyy-MM-dd'
    $newSection = "## [$version] - $today`r`n"
    if ($groups.Added.Count   -gt 0) { $newSection += "### Added`r`n"   + ($groups.Added   -join "`r`n") + "`r`n" }
    if ($groups.Fixed.Count   -gt 0) { $newSection += "### Fixed`r`n"   + ($groups.Fixed   -join "`r`n") + "`r`n" }
    if ($groups.Changed.Count -gt 0) { $newSection += "### Changed`r`n" + ($groups.Changed -join "`r`n") + "`r`n" }

    if (-not (Test-Path $changelog)) {
        $content = "## [Unreleased]`r`n`r`n$newSection"
        Set-Content -Path $changelog -Value $content -Encoding UTF8
    } else {
        $old = Get-Content $changelog -Raw
        if ($old -notmatch '## \[Unreleased\]') { $old = "## [Unreleased]`r`n`r`n" + $old }
        $updated = [regex]::Replace($old, '(## \[Unreleased\])', "`$1`r`n`r`n$newSection", 1)
        Set-Content -Path $changelog -Value $updated -Encoding UTF8
    }
}

function Update-Files {
    param($version)

    # Update VSIX manifest version
    [xml]$mxml = Get-Content $manifest
    $identity = $mxml.PackageManifest.Metadata.Identity
    if (-not $identity) { throw "Could not find Identity in manifest." }
    $identity.Version = $version
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $sw = New-Object System.IO.StreamWriter($manifest, $false, $utf8)
    $mxml.Save($sw); $sw.Dispose()

    # Update source.extension.cs version
    if (-not (Test-Path $vsix)) { throw "source.extension.cs not found: $vsix" }
    $src = Get-Content $vsix -Raw
    $updatedSrc = [regex]::Replace(
        $src,
        'public const string Version = "\d+\.\d+\.\d+";',
        "public const string Version = `"$version`";"
    )
    Set-Content -Path $vsix -Value $updatedSrc -Encoding UTF8
}

function Commit-And-Tag {
    param($version)
    git add -- $manifest $vsix $changelog | Out-Null
    $commitMsg = "chore(release): v$version"
    git commit -m $commitMsg
    git tag "v$version"
    Write-Host ""
    Write-Host "✔ Release v$version created."
    Write-Host "   Push with: git push --follow-tags"
}

function Main {
    $currentVersion = Get-VersionFromChangelog $changelog
    if (-not $currentVersion) { $currentVersion = Get-VersionFromManifest $manifest }
    Write-Host "Current version: $currentVersion"

    $newVersion = Bump-Version $currentVersion
    Write-Host "New version: $newVersion"

    $rawCommits = Collect-Commits
    $groups = Group-Commits $rawCommits
    Update-Changelog $newVersion $groups
    Update-Files $newVersion
    Commit-And-Tag $newVersion
}

Main
