param(
    [Parameter(Mandatory=$true)]
    [string]$Pattern
)

$files = powershell.exe -File .github/mine.ps1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Set-Location Codescene.VSExtension.VS2022
    $output = dotnet.exe format analyzers Codescene.VSExtension.sln --severity info --no-restore --include $files 2>&1
    $formatExit = $LASTEXITCODE
    Set-Location ..

    if ($formatExit -ne 0) {
        Write-Host $output
        exit 1
    }

    $warnings = $output | Select-String -Pattern $Pattern | ForEach-Object { $_.Line } | Sort-Object -Unique
    if ($warnings) {
        $warnings | ForEach-Object { Write-Host $_ }
        exit 1
    } else {
        exit 0
    }
} else {
    Write-Host 'No changed C# files to check' -ForegroundColor Yellow
    exit 0
}
