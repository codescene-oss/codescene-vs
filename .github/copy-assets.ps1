$webDir = "Codescene.VSExtension.VS2022/ToolWindows/WebComponent"
$vsixDir = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022"

New-Item -ItemType Directory -Force -Path $webDir | Out-Null

Copy-Item cs-cwf/index.css "$vsixDir/ToolWindows/WebComponent/" -Force
Copy-Item cs-cwf/index.js  "$vsixDir/ToolWindows/WebComponent/" -Force

Copy-Item "./cs-ide/cs-ide.exe" $vsixDir -Force
