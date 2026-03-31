param(
    [Parameter(Mandatory=$true)]
    [string]$LogFile
)

$content = Get-Content $LogFile | Where-Object {
    $_ -notmatch 'SourceItems item:' -and
    $_ -notmatch 'csc\.exe\s+' -and
    $_ -notmatch "BuildResponseFile = '"
}

$content | ForEach-Object { Write-Host $_ }
