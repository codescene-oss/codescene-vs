$ErrorActionPreference = "Stop"

$baseBranch = if ($env:GITHUB_BASE_REF) { "origin/$env:GITHUB_BASE_REF" } else { "main" }

$newFileThreshold = 12
$newFilesLocThreshold = 2250
$modifiedFilesLocThreshold = 2250
$combinedLocThreshold = 2750

try {
    $gitRoot = git rev-parse --show-toplevel 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Not in a git repository" -ForegroundColor Yellow
        exit 0
    }

    $currentBranch = git rev-parse --abbrev-ref HEAD
    if ($currentBranch -eq "main" -or $currentBranch -eq "master") {
        Write-Host "On main/master branch, skipping PR size check" -ForegroundColor Yellow
        exit 0
    }

    $diffOutput = git diff --name-status "$baseBranch...HEAD" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Could not compare with base branch: $baseBranch" -ForegroundColor Yellow
        exit 0
    }

    $lines = $diffOutput -split "`n" | Where-Object { $_ -match '\S' }

    $newFiles = @()
    $modifiedFiles = @()

    foreach ($line in $lines) {
        $parts = $line -split "`t"
        $status = $parts[0].Trim()
        $filePath = $parts[1].Trim()

        if ($filePath -notmatch '\.cs$') {
            continue
        }

        if ($status -match '^A') {
            $newFiles += $filePath
        }
        elseif ($status -match '^M') {
            $modifiedFiles += $filePath
        }
    }

    $newFileCount = $newFiles.Count
    $newFilesLoc = 0

    foreach ($file in $newFiles) {
        try {
            $content = git show "HEAD:$file" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $lineCount = ($content | Measure-Object -Line).Lines
                $newFilesLoc += $lineCount
            }
        }
        catch {
        }
    }

    $modifiedFilesLoc = 0

    if ($modifiedFiles.Count -gt 0) {
        $numstatOutput = git diff --numstat "$baseBranch...HEAD" -- $modifiedFiles
        foreach ($line in $numstatOutput -split "`n") {
            if ($line -match '^\d+\s+\d+') {
                $parts = $line -split '\s+'
                $additions = [int]$parts[0]
                $deletions = [int]$parts[1]
                $modifiedFilesLoc += ($additions + $deletions)
            }
        }
    }

    $combinedLoc = $newFilesLoc + $modifiedFilesLoc

    $violations = @()

    if ($newFileCount -gt $newFileThreshold) {
        $violations += "New files: $newFileCount exceeds threshold of $newFileThreshold"
    }

    if ($newFilesLoc -gt $newFilesLocThreshold) {
        $violations += "New files LOC: $newFilesLoc exceeds threshold of $newFilesLocThreshold"
    }

    if ($modifiedFilesLoc -gt $modifiedFilesLocThreshold) {
        $violations += "Modified files LOC: $modifiedFilesLoc exceeds threshold of $modifiedFilesLocThreshold"
    }

    if ($combinedLoc -gt $combinedLocThreshold) {
        $violations += "Combined LOC: $combinedLoc exceeds threshold of $combinedLocThreshold"
    }

    Write-Host "PR Size Check Results:" -ForegroundColor Cyan
    Write-Host "  New files: $newFileCount/$newFileThreshold"
    Write-Host "  New files LOC: $newFilesLoc/$newFilesLocThreshold"
    Write-Host "  Modified files LOC: $modifiedFilesLoc/$modifiedFilesLocThreshold"
    Write-Host "  Combined LOC: $combinedLoc/$combinedLocThreshold"

    if ($violations.Count -gt 0) {
        Write-Host ""
        Write-Host "VIOLATIONS:" -ForegroundColor Red
        foreach ($violation in $violations) {
            Write-Host "  - $violation" -ForegroundColor Red
        }
        exit 1
    }
    else {
        Write-Host ""
        Write-Host "All PR size checks passed!" -ForegroundColor Green
        exit 0
    }
}
catch {
    Write-Host "Error during PR size check: $_" -ForegroundColor Yellow
    exit 0
}
