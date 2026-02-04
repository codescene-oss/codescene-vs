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
    $buildExitCode = $LASTEXITCODE

    $content = Get-Content 'analyzers.log' | Where-Object {
        $_ -notmatch 'SourceItems item:' -and
        $_ -notmatch 'csc\.exe\s+' -and
        $_ -notmatch "BuildResponseFile = '"
    }
    $content | Set-Content 'analyzers.log'

    if ($buildExitCode -ne 0) {
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
    exit 0
}
