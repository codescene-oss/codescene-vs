param(
    [switch]$Local
)

function Exit-WithError($Message) {
    Write-Error $Message
    exit 1
}

function Get-CwfVersionOverride {
    $candidate = "$env:CWF_VERSION".TrimStart([char]0xFEFF).Trim()
    if ($candidate) { return $candidate }
    return $null
}

function Read-CwfPinFile {
    if (-not (Test-Path cwf.version)) {
        Exit-WithError "No CWF version found: cwf.version does not exist and CWF_VERSION is not set."
    }
    return "$(Get-Content cwf.version -Raw)".TrimStart([char]0xFEFF).Trim()
}

function Resolve-CwfVersion {
    $value = Get-CwfVersionOverride
    $source = "CWF_VERSION"
    if (-not $value) {
        $value = Read-CwfPinFile
        $source = "cwf.version"
    }

    if ($value -cnotmatch '^v?[0-9]+\.[0-9]+\.[0-9]+$') {
        Exit-WithError "Invalid CWF version '$value' from $source. Expected MAJOR.MINOR.PATCH, optionally prefixed with v."
    }

    return $value.TrimStart('v')
}

function Get-HttpStatus($err) {
    if ($err.Exception.Response) {
        return [int]$err.Exception.Response.StatusCode
    }
    return $null
}

function Format-CwfFailure($Step, $Err, $Status) {
    if ($null -eq $Status) {
        return "$Step failed: $($Err.Exception.Message)"
    }
    $message = "$Step failed with HTTP $Status."
    if ($Status -eq 401 -or $Status -eq 403) {
        $message += " Check that CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN is valid and has access to empear-analytics/cs-webview."
    }
    return $message
}

function Invoke-LocalBuild($LocalCwf) {
    Write-Host "Building CWF locally from $LocalCwf..."
    Push-Location $LocalCwf
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}

function Copy-LocalBuild($AssetsPath) {
    if (Test-Path cs-cwf) { Remove-Item cs-cwf -Recurse -Force }
    New-Item -ItemType Directory -Path cs-cwf | Out-Null
    Copy-Item (Join-Path $AssetsPath "*") cs-cwf
    $webDir = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/ToolWindows/WebComponent"
    Copy-Item cs-cwf/index.css $webDir -Force
    Copy-Item cs-cwf/index.js $webDir -Force
    if (Test-Path cs-cwf.zip) { Remove-Item cs-cwf.zip -Force }
    Compress-Archive -Path "cs-cwf\*" -DestinationPath cs-cwf.zip
    Set-Content -Path cs-cwf/.cwf-version -Value "local" -Encoding ascii -NoNewline
}

function Install-LocalCwf {
    $localCwf = Join-Path (Get-Location) "..\cs-webview"
    if (-not (Test-Path (Join-Path $localCwf "package.json"))) {
        Exit-WithError "Local CWF checkout not found at $localCwf"
    }

    Invoke-LocalBuild $localCwf

    $assetsPath = Join-Path $localCwf "build\assets"
    if (-not (Test-Path $assetsPath)) {
        Exit-WithError "Local CWF build did not produce build\assets."
    }
    Copy-LocalBuild $assetsPath
}

function Test-CwfPresent($Version) {
    $stampPath = "cs-cwf/.cwf-version"
    if (-not ((Test-Path $stampPath) -and (Test-Path "cs-cwf/index.js"))) {
        return $false
    }
    return ([string](Get-Content $stampPath -Raw)).Trim() -eq $Version
}

function Get-CwfToken($Version) {
    $token = $env:CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        Exit-WithError "CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN is not set. It is required to download cs-webview v$Version."
    }
    return $token
}

function New-CwfHeaders($Token, $Accept) {
    return @{
        Authorization = "token $Token"
        Accept        = $Accept
        "User-Agent"  = "actions"
    }
}

function Get-CwfRelease($Tag, $Token) {
    Write-Host "Fetching cs-webview release $Tag..."
    try {
        return Invoke-RestMethod -ErrorAction Stop -Uri "https://api.github.com/repos/empear-analytics/cs-webview/releases/tags/$Tag" -Headers (New-CwfHeaders $Token "application/vnd.github+json")
    }
    catch {
        $status = Get-HttpStatus $_
        if ($status -eq 404) {
            Exit-WithError "Release tag $Tag not found in empear-analytics/cs-webview (HTTP 404)."
        }
        Exit-WithError (Format-CwfFailure -Step "release lookup" -Err $_ -Status $status)
    }
}

function Get-CwfAsset($Release, $Tag) {
    $assetName = "cs-webview-$Tag.zip"
    $asset = $Release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { Exit-WithError "Asset $assetName not found in release $Tag." }
    return $asset
}

function Save-CwfAsset($Asset, $Token) {
    Write-Host "Downloading asset via API: $($Asset.name)"
    try {
        Invoke-WebRequest -ErrorAction Stop -Uri $Asset.url -Headers (New-CwfHeaders $Token "application/octet-stream") -OutFile cs-cwf.zip
    }
    catch {
        Exit-WithError (Format-CwfFailure -Step "asset download" -Err $_ -Status (Get-HttpStatus $_))
    }
}

function Expand-CwfArchive {
    if (Test-Path cs-cwf-temp) { Remove-Item cs-cwf-temp -Recurse -Force }
    Expand-Archive -Path cs-cwf.zip -DestinationPath cs-cwf-temp -Force
    if (Test-Path cs-cwf) { Remove-Item cs-cwf -Recurse -Force }
    New-Item -ItemType Directory -Path cs-cwf | Out-Null

    $assetsPath = Join-Path "cs-cwf-temp" "assets"
    if (Test-Path $assetsPath) {
        Get-ChildItem $assetsPath -File | Move-Item -Destination cs-cwf
    }

    Remove-Item cs-cwf-temp -Recurse -Force
}

function Install-ReleaseCwf($Version) {
    $token = Get-CwfToken $Version
    $tag = "v$Version"
    $release = Get-CwfRelease $tag $token
    Save-CwfAsset (Get-CwfAsset $release $tag) $token

    $stampPath = "cs-cwf/.cwf-version"
    if (Test-Path $stampPath) { Remove-Item $stampPath -Force }
    Expand-CwfArchive
    if (-not (Test-Path "cs-cwf/index.js")) {
        Exit-WithError "CWF $Version extraction did not produce cs-cwf/index.js."
    }
    Set-Content -Path $stampPath -Value $Version -Encoding ascii -NoNewline
}

function Main($UseLocal) {
    if ($UseLocal) {
        Install-LocalCwf
        exit 0
    }

    $version = Resolve-CwfVersion
    if (Test-CwfPresent $version) {
        Write-Host "CWF $version already present, skipping download"
        exit 0
    }
    Install-ReleaseCwf $version
}

Main $Local.IsPresent
