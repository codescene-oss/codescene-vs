$status = git status --short --porcelain | ForEach-Object { $_.Substring(3) }
$diff = git diff --name-only main...HEAD
$allFiles = @($status) + @($diff) | Where-Object { $_ -ne $null -and $_ -ne '' } | Select-Object -Unique
$files = $allFiles | Where-Object { $_ -match '\.cs$' }

if ($files) {
    dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln --include ($files -join ' ')
} else {
    Write-Host 'No C# files to format'
}

exit $LASTEXITCODE
