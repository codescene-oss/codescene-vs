# Runs Core.Tests in Debug configuration 20 times without building.
# Use to check for flaky tests. Run from repo root.

$ErrorActionPreference = 'Stop'
$runs = 20
$root = $PSScriptRoot
$searchPath = Join-Path $root 'Codescene.VSExtension.VS2022'

$files = Get-ChildItem -Recurse -Filter '*Tests.dll' -Path $searchPath -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'bin\\Debug' } |
    Select-Object -ExpandProperty FullName

if (-not $files) {
    Write-Error "No *Tests.dll found under $searchPath\bin\Debug. Build the solution in Debug first."
    exit 1
}

$passed = 0
$failed = 0
$failRuns = @()

for ($i = 1; $i -le $runs; $i++) {
    $out = & vstest.console.exe $files /logger:trx 2>&1
    if ($LASTEXITCODE -eq 0) {
        $passed++
        Write-Host "Run $i/$runs : PASS"
    } else {
        $failed++
        $failRuns += $i
        Write-Host "Run $i/$runs : FAIL"
        $out | Where-Object { $_ -match '\s+Failed\s|Test Run Failed|Failed:\s+\d' } | Write-Host
    }
}

Write-Host ""
Write-Host "Summary: $passed/$runs passed, $failed failed."
if ($failRuns.Count -gt 0) {
    Write-Host "Failed runs: $($failRuns -join ', ')"
    exit 1
}
