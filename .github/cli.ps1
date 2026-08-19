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

$url = "https://downloads.codescene.io/enterprise/cli/cs-ide-jre-windows-amd64-$RequiredDevToolVersion.zip"
Write-Host "Downloading from $url"
Invoke-WebRequest -Uri $url -OutFile cs-ide.zip

if (Test-Path ./cs-ide) {
    Remove-Item ./cs-ide -Recurse -Force
}
Expand-Archive -Path cs-ide.zip -DestinationPath ./cs-ide -Force

$distRoot = $null
if (Test-Path "./cs-ide/cs-windows-amd64/cs-ide.jar") {
    $distRoot = "./cs-ide/cs-windows-amd64"
} elseif (Test-Path "./cs-ide/cs-ide.jar") {
    $distRoot = "./cs-ide"
} else {
    $nested = Get-ChildItem -Path ./cs-ide -Directory | Where-Object { Test-Path (Join-Path $_.FullName "cs-ide.jar") } | Select-Object -First 1
    if ($nested) {
        $distRoot = $nested.FullName
    }
}

if (-not $distRoot) {
    Write-Error "Downloaded CLI zip did not contain cs-ide.jar."
    exit 1
}

Write-Host "CLI distribution root: $distRoot"
