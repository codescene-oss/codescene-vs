function Report-AndExit {
    param(
        [string[]]$Violations
    )

    if ($Violations.Count -gt 0) {
        $Violations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        exit 1
    } else {
        exit 0
    }
}

$changedFiles = pwsh.exe -File .github/mine.ps1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    $fileList = $changedFiles -split ' '
    $violations = @()

    foreach ($file in $fileList) {
        if (Test-Path $file) {
            $lineCount = (Get-Content $file | Measure-Object -Line).Lines
            if ($lineCount -gt 350) {
                $violations += "$file has $lineCount lines (max 350 allowed). Consider splitting the file into smaller classes."
            }
        }
    }

    Report-AndExit -Violations $violations
} else {
    exit 0
}
