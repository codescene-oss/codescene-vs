$status = git status --short --porcelain | ForEach-Object {
    $path = $_.Substring(3)
    if ($path -match ' -> ') {
        $path = $path -replace '^.* -> ', ''
    }
    $path
}
$diff = git diff --name-only main...HEAD
$allFiles = @($status) + @($diff) | Where-Object { $_ -ne $null -and $_ -ne '' } | Select-Object -Unique
$csFiles = $allFiles | Where-Object { $_ -match '\.cs$' }
$testFiles = $csFiles | Where-Object { $_ -match 'Tests\.cs$' }

if ($testFiles) {
    $testNames = $testFiles | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
    $filters = @($testNames | ForEach-Object { "FullyQualifiedName~$_" })
    $filter = $filters -join '|'
    dotnet test Codescene.VSExtension.VS2022/Codescene.VSExtension.sln -c Release --no-build --filter $filter --logger trx > test.log 2>&1

    if ($LASTEXITCODE -eq 0) {
        Get-Content test.log -Tail 4
    } else {
        Get-Content test.log
        exit 1
    }
}
