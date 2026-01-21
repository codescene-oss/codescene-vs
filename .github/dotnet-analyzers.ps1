Set-Location Codescene.VSExtension.VS2022
MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release

if ($LASTEXITCODE -eq 0) {
    Set-Location ..
    Select-String -Path 'dotnet-analyzers.log' -Pattern 'warning CA' | ForEach-Object { $_.Line } | Sort-Object -Unique
} else {
    Set-Location ..
    Get-Content dotnet-analyzers.log
    exit 1
}
