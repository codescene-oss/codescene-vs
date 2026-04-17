dotnet test Codescene.VSExtension.VS2022/Codescene.VSExtension.sln -c Release --no-build --logger trx > test.log 2>&1

if ($LASTEXITCODE -eq 0) {
    Get-Content test.log -Tail 4
} else {
    Get-Content test.log
    exit 1
}
