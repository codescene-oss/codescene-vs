param(
    [switch]$Local
)

$localCwf = Join-Path (Get-Location) "..\cs-webview"
$localPackage = Join-Path $localCwf "package.json"

if ($Local) {
    if (-not (Test-Path $localPackage)) {
        Write-Error "Local CWF checkout not found at $localCwf"
        exit 1
    }

    Write-Host "Building CWF locally from $localCwf..."
    Push-Location $localCwf
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }

    $assetsPath = Join-Path $localCwf "build\assets"
    if (-not (Test-Path $assetsPath)) {
        Write-Error "Local CWF build did not produce build\assets."
        exit 1
    }

    if (Test-Path cs-cwf) { Remove-Item cs-cwf -Recurse -Force }
    New-Item -ItemType Directory -Path cs-cwf | Out-Null
    Copy-Item (Join-Path $assetsPath "*") cs-cwf
    $webDir = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/ToolWindows/WebComponent"
    Copy-Item cs-cwf/index.css $webDir -Force
    Copy-Item cs-cwf/index.js $webDir -Force
    if (Test-Path cs-cwf.zip) { Remove-Item cs-cwf.zip -Force }
    Compress-Archive -Path "cs-cwf\*" -DestinationPath cs-cwf.zip
    exit 0
}

$Token = $env:CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN

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