$changedFiles = pwsh.exe -File .github/mine.ps1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    $fileList = $changedFiles -split ' '
    $violations = @()

    foreach ($file in $fileList) {
        if (Test-Path $file) {
            $content = Get-Content $file -Raw
            if ($content -match '#endregion') {
                $violations += "$file contains #endregion. Don't use regions. Consider splitting the file into smaller classes."
            }
        }
    }

    if ($violations.Count -gt 0) {
        $violations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        exit 1
    } else {
        exit 0
    }
} else {
    exit 0
}
