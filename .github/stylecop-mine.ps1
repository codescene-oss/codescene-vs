$status = git status --short --porcelain | ForEach-Object { $_.Substring(3) }
$diff = git diff --name-only main...HEAD
$allFiles = @($status) + @($diff) | Where-Object { $_ -ne $null -and $_ -ne '' } | Select-Object -Unique
$files = $allFiles | Where-Object { $_ -match '\.cs$' }

if ($files) {
    Set-Location Codescene.VSExtension.VS2022
    dotnet.exe format analyzers Codescene.VSExtension.sln --severity info --no-restore --include ($files -join ' ')
    $exitCode = $LASTEXITCODE
    Set-Location ..

    if ($exitCode -eq 0) {
        Select-String -Path 'stylecop.log' -Pattern 'warning SA' | ForEach-Object { $_.Line } | Sort-Object -Unique
    } else {
        Get-Content stylecop.log
        exit 1
    }
} else {
    Write-Host 'No changed C# files to check' -ForegroundColor Yellow
}
