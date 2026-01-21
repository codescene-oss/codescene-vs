SHELL := cmd.exe

include cache.mk
include sha.mk

.PHONY: test test1 test-mine copy-assets restore format format-all format-check stylecop stylecop-mine dotnet-analyzers dotnet-analyzers-mine test-cache test-sha

# You might need something like:
# export PATH="$PATH:/mnt/c/Program Files/dotnet:/mnt/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin:/mnt/c/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/Extensions/TestPlatform"

# NOTE: tasks default to silenced output (unless they fail) for AI efficency.

# NOTE: Make's wildcard function doesn't support ** for recursive matching like other glob implementations.
# We must explicitly specify multiple directory depth levels to track all .cs files.
CS_FILES := $(wildcard Codescene.VSExtension.VS2022/*.cs) \
            $(wildcard Codescene.VSExtension.VS2022/**/*.cs) \
            $(wildcard Codescene.VSExtension.VS2022/**/**/*.cs) \
            $(wildcard Codescene.VSExtension.VS2022/**/**/**/*.cs) \
            $(wildcard Codescene.VSExtension.VS2022/**/**/**/**/*.cs)

PROJ_FILES := $(wildcard Codescene.VSExtension.VS2022/*.csproj) \
              $(wildcard Codescene.VSExtension.VS2022/*.sln) \
              $(wildcard Codescene.VSExtension.VS2022/**/*.csproj) \
              $(wildcard Codescene.VSExtension.VS2022/**/**/*.csproj)

cs-cwf.zip:
	@powershell.exe .github/cwf.ps1 -Token "$$CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN" > cs-cwf.log 2>&1 && del cs-cwf.log || (type cs-cwf.log && del cs-cwf.log && exit /b 1)

cs-ide.zip:
	@powershell.exe .github/cli.ps1 > cs-ide.log 2>&1 && del cs-ide.log || (type cs-ide.log && del cs-ide.log && exit /b 1)

copy-assets: cs-cwf.zip cs-ide.zip
	@powershell.exe .github/copy-assets.ps1 > copy-assets.log 2>&1 && del copy-assets.log || (type copy-assets.log && del copy-assets.log && exit /b 1)

restore:
	@dotnet.exe restore Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > restore.log 2>&1 && del restore.log || (type restore.log && del restore.log && exit /b 1)

# Build only runs if source files or assets are newer than .build-timestamp
.build-timestamp: $(CS_FILES) $(PROJ_FILES) cs-cwf.zip cs-ide.zip
	@dotnet.exe restore Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > restore.log 2>&1 && del restore.log || (type restore.log && del restore.log && exit /b 1)
	@cd Codescene.VSExtension.VS2022 && MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release > build.log 2>&1 && del build.log || (type build.log && del build.log && exit /b 1)
	@echo Build completed at %date% %time% > .build-timestamp

build: .build-timestamp

test: build
	@powershell.exe -File .github/test.ps1 > test.log 2>&1 && del test.log || (type test.log && del test.log && exit /b 1)

# make test1 TEST=GitChangeObserverTests
test1: build
	@powershell.exe -File .github/test1.ps1 -TestName $(TEST) > test.log 2>&1 && del test.log || (type test.log && del test.log && exit /b 1)

# Runs tests for changed *Tests.cs files (per Git)
test-mine: build
	@powershell.exe -File .github/test-mine.ps1 > test.log 2>&1 && del test.log || (type test.log && del test.log && exit /b 1)

# Formats just the .cs files you've worked on (per Git)
format:
	@powershell.exe -File .github/format-mine.ps1 > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

# Formats all files.
format-all:
	@dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

format-check:
	@dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln --verify-no-changes > format-check.log 2>&1 && del format-check.log || (type format-check.log && del format-check.log && exit /b 1)

stylecop: restore
	@powershell.exe -File .github/stylecop.ps1 > stylecop.log 2>&1 && del stylecop.log || (type stylecop.log && del stylecop.log && exit /b 1)

stylecop-mine: restore
	@powershell.exe -File .github/stylecop-mine.ps1 > stylecop.log 2>&1 && del stylecop.log || (type stylecop.log && del stylecop.log && exit /b 1)

dotnet-analyzers: restore
	@powershell.exe -File .github/dotnet-analyzers.ps1 > dotnet-analyzers.log 2>&1 && del dotnet-analyzers.log || (type dotnet-analyzers.log && del dotnet-analyzers.log && exit /b 1)

dotnet-analyzers-mine: restore
	@powershell.exe -File .github/dotnet-analyzers-mine.ps1 > dotnet-analyzers.log 2>&1 && del dotnet-analyzers.log || (type dotnet-analyzers.log && del dotnet-analyzers.log && exit /b 1)

# iter - iterate. Good as a promopt: "iterate to success using `make iter`"
iter: format dotnet-analyzers-mine stylecop-mine test-mine
