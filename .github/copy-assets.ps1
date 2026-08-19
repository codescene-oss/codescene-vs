$vsixDir = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022"
$webDir = "$vsixDir/ToolWindows/WebComponent"

New-Item -ItemType Directory -Force -Path $webDir | Out-Null

Copy-Item cs-cwf/index.css $webDir -Force
Copy-Item cs-cwf/index.js  $webDir -Force

$distTarget = "$vsixDir/cs-windows-amd64"
$distSource = $null
if (Test-Path "./cs-ide/cs-windows-amd64/cs-ide.jar") {
    $distSource = "./cs-ide/cs-windows-amd64"
} elseif (Test-Path "./cs-ide/cs-ide.jar") {
    $distSource = "./cs-ide"
} else {
    $nested = Get-ChildItem -Path ./cs-ide -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path (Join-Path $_.FullName "cs-ide.jar") } | Select-Object -First 1
    if ($nested) {
        $distSource = $nested.FullName
    }
}

if (-not $distSource) {
    Write-Error "No cs-ide.jar distribution found to copy into the VSIX project."
    exit 1
}

if (Test-Path $distTarget) {
    Remove-Item $distTarget -Recurse -Force
}
Copy-Item $distSource $distTarget -Recurse -Force
