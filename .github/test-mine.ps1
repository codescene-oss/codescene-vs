$status = git status --short --porcelain | ForEach-Object { $_.Substring(3) }
$diff = git diff --name-only main...HEAD
$allFiles = @($status) + @($diff) | Where-Object { $_ -ne $null -and $_ -ne '' } | Select-Object -Unique
$csFiles = $allFiles | Where-Object { $_ -match '\.cs$' }
$testFiles = $csFiles | Where-Object { $_ -match 'Tests\.cs$' }

if ($testFiles) {
    $testNames = $testFiles | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
    $testDlls = Get-ChildItem -Recurse -Filter '*Tests.dll' -Path 'Codescene.VSExtension.VS2022' | Where-Object { $_.FullName -match 'bin\\Release' } | Select-Object -ExpandProperty FullName
    $testNameStr = if ($testNames -is [array]) { $testNames -join ',' } else { $testNames }
    vstest.console.exe $testDlls /Tests:$testNameStr /logger:trx > test.log 2>&1

    if ($LASTEXITCODE -eq 0) {
        Get-Content test.log -Tail 4
    } else {
        Get-Content test.log
        exit 1
    }
}
