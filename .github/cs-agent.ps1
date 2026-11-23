function Exit-WithError($Message) {
    Write-Error $Message
    exit 1
}

function Read-VersionPin {
    $cliSettingsPath = "Codescene.VSExtension.VS2022/Codescene.VSExtension.Core/Application/Cli/CliSettingsProvider.cs"

    if (-not (Test-Path $cliSettingsPath)) {
        Exit-WithError "CliSettingsProvider.cs not found at: $cliSettingsPath"
    }

    $content = Get-Content $cliSettingsPath -Raw

    if ($content -match 'RequiredAgentVersion\s*=>\s*"([^"]+)"') {
        $version = $matches[1]
    } else {
        Exit-WithError "Could not extract RequiredAgentVersion from CliSettingsProvider.cs"
    }

    if ($content -match 'RequiredAgentBinarySha256\s*=>\s*"([^"]+)"') {
        $sha = $matches[1]
    } else {
        Exit-WithError "Could not extract RequiredAgentBinarySha256 from CliSettingsProvider.cs"
    }

    return @{ Version = $version; Sha = $sha }
}

function Get-HttpStatus($err) {
    if ($err.Exception.Response) {
        return [int]$err.Exception.Response.StatusCode
    }
    return $null
}

function Format-AgentFailure($Step, $Err, $Status) {
    if ($null -eq $Status) {
        return "$Step failed: $($Err.Exception.Message)"
    }
    $message = "$Step failed with HTTP $Status."
    if (@(401, 403, 404) -contains $Status) {
        $message += " Check that CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN is valid and has access to empear-analytics/cs-agent."
    }
    return $message
}

function Test-AgentPresent($Version, $Sha) {
    $stampPath = "cs-agent/.cs-agent-version"
    $exePath = "cs-agent/cs-agent.exe"
    if (-not ((Test-Path $stampPath) -and (Test-Path $exePath))) {
        return $false
    }
    $stamp = ([string](Get-Content $stampPath -Raw)).Trim()
    if ($stamp -ne $Version) {
        return $false
    }
    $actualSha = (Get-FileHash $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
    return $actualSha -eq $Sha
}

function Get-AgentToken($Version) {
    $token = $env:CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        Exit-WithError "CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN is not set. It is required to download cs-agent v$Version from empear-analytics/cs-agent."
    }
    return $token
}

function New-AgentHeaders($Token, $Accept) {
    return @{
        Authorization = "token $Token"
        Accept        = $Accept
        "User-Agent"  = "actions"
    }
}

function Get-AgentRelease($Tag, $Token) {
    Write-Host "Fetching cs-agent release $Tag..."
    try {
        return Invoke-RestMethod -ErrorAction Stop -Uri "https://api.github.com/repos/empear-analytics/cs-agent/releases/tags/$Tag" -Headers (New-AgentHeaders $Token "application/vnd.github+json")
    }
    catch {
        $status = Get-HttpStatus $_
        if ($status -eq 404) {
            Exit-WithError "Release tag $Tag not found in empear-analytics/cs-agent (HTTP 404)."
        }
        Exit-WithError (Format-AgentFailure -Step "release lookup" -Err $_ -Status $status)
    }
}

function Get-AgentAsset($Release, $Tag) {
    $assetName = "refactoring-agent-windows-amd64.zip"
    $asset = $Release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { Exit-WithError "Asset $assetName not found in release $Tag." }
    return $asset
}

function Save-AgentAsset($Asset, $Token) {
    Write-Host "Downloading asset via API: $($Asset.name)"
    try {
        Invoke-WebRequest -ErrorAction Stop -Uri $Asset.url -Headers (New-AgentHeaders $Token "application/octet-stream") -OutFile cs-agent.zip
    }
    catch {
        Exit-WithError (Format-AgentFailure -Step "asset download" -Err $_ -Status (Get-HttpStatus $_))
    }
}

function Expand-AgentArchive {
    if (Test-Path cs-agent-temp) { Remove-Item cs-agent-temp -Recurse -Force }
    Expand-Archive -Path cs-agent.zip -DestinationPath cs-agent-temp -Force
    if (Test-Path cs-agent) { Remove-Item cs-agent -Recurse -Force }
    New-Item -ItemType Directory -Path cs-agent | Out-Null

    $exePath = Join-Path "cs-agent-temp" "refactoring-agent-windows-amd64.exe"
    if (-not (Test-Path $exePath)) {
        Exit-WithError "Agent archive did not contain refactoring-agent-windows-amd64.exe."
    }
    Move-Item $exePath (Join-Path "cs-agent" "cs-agent.exe")

    Remove-Item cs-agent-temp -Recurse -Force
}

function Install-ReleaseAgent($Version, $Sha) {
    $token = Get-AgentToken $Version
    $tag = "v$Version"
    $release = Get-AgentRelease $tag $token
    Save-AgentAsset (Get-AgentAsset $release $tag) $token

    $stampPath = "cs-agent/.cs-agent-version"
    if (Test-Path $stampPath) { Remove-Item $stampPath -Force }
    Expand-AgentArchive
    if (-not (Test-Path "cs-agent/cs-agent.exe")) {
        Exit-WithError "Agent $Version extraction did not produce cs-agent/cs-agent.exe."
    }

    $actualSha = (Get-FileHash "cs-agent/cs-agent.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Bundled cs-agent SHA-256: $actualSha"
    if ($actualSha -ne $Sha) {
        Exit-WithError "SHA-256 mismatch: expected $Sha but got $actualSha. Update RequiredAgentBinarySha256 in CliSettingsProvider.cs."
    }

    Set-Content -Path $stampPath -Value $Version -Encoding ascii -NoNewline
}

function Main {
    $pin = Read-VersionPin
    if (Test-AgentPresent $pin.Version $pin.Sha) {
        Write-Host "cs-agent $($pin.Version) with matching SHA already present, skipping download"
        exit 0
    }
    Install-ReleaseAgent $pin.Version $pin.Sha
}

Main
