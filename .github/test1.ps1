param(
    [Parameter(Mandatory=$true)]
    [string]$TestName
)

$files = Get-ChildItem -Recurse -Filter '*Tests.dll' -Path 'Codescene.VSExtension.VS2022' | Where-Object { $_.FullName -match 'bin\\Release' } | Select-Object -ExpandProperty FullName
vstest.console.exe $files /Tests:$TestName /logger:trx

if ($LASTEXITCODE -eq 0) {
    Get-Content test.log -Tail 4
} else {
    Get-Content test.log
    exit 1
}
