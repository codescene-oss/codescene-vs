.PHONY: build test copy-assets restore

# Convenience file for WSL users.

# You might need something like:
# export PATH="$PATH:/mnt/c/Program Files/dotnet:/mnt/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin:/mnt/c/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/Extensions/TestPlatform"

cs-cwf.zip:
	powershell.exe .github/cwf.ps1 -Token "$$CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN" 

cs-ide.zip:
	powershell.exe .github/cli.ps1

copy-assets: cs-cwf.zip cs-ide.zip
	powershell.exe .github/copy-assets.ps1

restore:
	dotnet.exe restore Codescene.VSExtension.VS2022/Codescene.VSExtension.sln

build: copy-assets restore
	MSBuild.exe Codescene.VSExtension.VS2022/Codescene.VSExtension.sln /p:Configuration=Release /p:Platform="Any CPU"

test: build
	vstest.console.exe '**\bin\Release\*Tests.dll' /logger:trx
