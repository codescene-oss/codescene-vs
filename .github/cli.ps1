$cliSettingsPath = "Codescene.VSExtension.VS2022/Codescene.VSExtension.Core/Application/Cli/CliSettingsProvider.cs"

if (-not (Test-Path $cliSettingsPath)) {
    Write-Error "CliSettingsProvider.cs not found at: $cliSettingsPath"
    Write-Host "Current directory: $(Get-Location)"
    Write-Host "Available files:"
    Get-ChildItem -Recurse -Filter "CliSettingsProvider.cs" | Select-Object -ExpandProperty FullName
    exit 1
}

$content = Get-Content $cliSettingsPath -Raw

if ($content -match 'RequiredDevToolVersion\s*=>\s*"([^"]+)"') {
    $RequiredDevToolVersion = $matches[1]
    Write-Host "Found CLI version: $RequiredDevToolVersion"
} else {
    Write-Error "Could not extract RequiredDevToolVersion from CliSettingsProvider.cs"
    exit 1
}

$url = "https://downloads.codescene.io/enterprise/cli/cs-ide-windows-amd64-$RequiredDevToolVersion.zip"
Write-Host "Downloading from $url"
Invoke-WebRequest -Uri $url -OutFile cs-ide.zip
Expand-Archive -Path cs-ide.zip -DestinationPath ./cs-ide -Force

$cliExePath = "./cs-ide/cs-ide.exe"
$cliSha256 = (Get-FileHash $cliExePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Bundled CLI SHA-256: $cliSha256"
Write-Host "Update RequiredCliBinarySha256 in CliSettingsProvider.cs when RequiredDevToolVersion changes."

$contractDir = "./cs-ide-contract"
$localContract = $env:CS_IDE_CONTRACT_BUNDLE
try {
    if (Test-Path $contractDir) {
        Remove-Item -Recurse -Force $contractDir
    }
    if ($localContract) {
        if (-not (Test-Path $localContract)) {
            throw "CS_IDE_CONTRACT_BUNDLE $localContract does not exist"
        }
        Write-Host "Using contract bundle $localContract"
        if ((Get-Item $localContract).PSIsContainer) {
            Copy-Item -Path $localContract -Destination $contractDir -Recurse -Force
        } else {
            Expand-Archive -Path $localContract -DestinationPath $contractDir -Force
        }
    } else {
        $contractUrl = "https://downloads.codescene.io/enterprise/cli/cs-ide-contract-$RequiredDevToolVersion.zip"
        Write-Host "Downloading from $contractUrl"
        Invoke-WebRequest -Uri $contractUrl -OutFile cs-ide-contract.zip -ErrorAction Stop
        Expand-Archive -Path cs-ide-contract.zip -DestinationPath $contractDir -Force
    }
    if (-not (Test-Path "$contractDir/wire-manifest.json")) {
        throw "$contractDir/wire-manifest.json is missing, this is not a contract bundle"
    }
} catch {
    Write-Error "Could not fetch the cs-ide contract bundle: $_"
    exit 1
}
