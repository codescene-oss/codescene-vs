$changedFiles = powershell.exe -File .github/mine.ps1
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

    if ($violations.Count -gt 0) {
        $violations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        exit 1
    } else {
        exit 0
    }
} else {
    Write-Host 'No changed C# files to check' -ForegroundColor Yellow
    exit 0
}
