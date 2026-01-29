param(
    [Parameter(Mandatory=$true)]
    [string]$Pattern,
    [string]$ExcludePattern
)

$changedFiles = pwsh.exe -File .github/mine.ps1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    $fileList = $changedFiles -split ' '

    make .run-analyzers > analyzers.log 2>&1
    if ($LASTEXITCODE -ne 0) {
        exit 1
    }

    $allWarnings = Select-String -Path 'analyzers.log' -Pattern $Pattern
    if ($ExcludePattern) {
        $allWarnings = $allWarnings | Where-Object { $_.Line -notmatch $ExcludePattern }
    }

    $filteredWarnings = $allWarnings | Where-Object {
        $line = $_.Line
        $fileList | Where-Object { $line -like "*$_*" }
    } | ForEach-Object { $_.Line } | Sort-Object -Unique

    if ($filteredWarnings) {
        $filteredWarnings | ForEach-Object { Write-Host $_ }
        exit 1
    } else {
        exit 0
    }
} else {
    Write-Host 'No changed C# files to check' -ForegroundColor Yellow
    exit 0
}
