$vsixDir = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022"
$webDir = "$vsixDir/ToolWindows/WebComponent"

New-Item -ItemType Directory -Force -Path $webDir | Out-Null

Copy-Item cs-cwf/index.css $webDir -Force
Copy-Item cs-cwf/index.js  $webDir -Force

Copy-Item "./cs-ide/cs-ide.exe" $vsixDir -Force
