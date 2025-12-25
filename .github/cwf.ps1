param(
    [Parameter(Mandatory=$true)]
    [string]$Token
)

$headers = @{
Authorization = "token $Token"
Accept        = "application/vnd.github+json"
"User-Agent"  = "actions"
}

Write-Host "Fetching latest release info..."
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/empear-analytics/cs-webview/releases/latest" -Headers $headers

$asset = $release.assets | Select-Object -First 1
if (-not $asset) { Write-Error "No assets found in the latest release."; exit 1 }

Write-Host "Downloading asset via API: $($asset.name)"
$downloadHeaders = @{
Authorization = "token $Token"
Accept        = "application/octet-stream"
"User-Agent"  = "actions"
}
Invoke-WebRequest -Uri "https://api.github.com/repos/empear-analytics/cs-webview/releases/assets/$($asset.id)" `
                -Headers $downloadHeaders `
                -OutFile cs-cwf.zip

Expand-Archive -Path cs-cwf.zip -DestinationPath cs-cwf-temp -Force
if (Test-Path cs-cwf) { Remove-Item cs-cwf -Recurse -Force }
New-Item -ItemType Directory -Path cs-cwf | Out-Null

$assetsPath = Join-Path "cs-cwf-temp" "assets"
if (Test-Path $assetsPath) {
    Get-ChildItem $assetsPath -File | Move-Item -Destination cs-cwf
}

Remove-Item cs-cwf-temp -Recurse -Force