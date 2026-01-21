Set-Location Codescene.VSExtension.VS2022
MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release -p:RunStyleCopAnalyzers=true

if ($LASTEXITCODE -eq 0) {
    Set-Location ..
    Select-String -Path 'stylecop.log' -Pattern 'warning SA' | ForEach-Object { $_.Line } | Sort-Object -Unique
} else {
    Set-Location ..
    Get-Content stylecop.log
    exit 1
}
