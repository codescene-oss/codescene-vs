SHELL := cmd.exe

include .github/cache.mk
include .github/sha.mk

# Lazy-once cache key - computed on first use, then cached for rest of Make invocation
CACHE_KEY = $(eval CACHE_KEY := $$(call get_cache_key))$(CACHE_KEY)

.PHONY: test test1 test-mine copy-assets restore format format-all format-check stylecop stylecop-mine dotnet-analyzers dotnet-analyzers-mine class-size-mine no-regions-mine test-cache test-sha install-cli delta .run-analyzers

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
	@pwsh.exe .github/cwf.ps1 > cs-cwf.log 2>&1 && del cs-cwf.log || (type cs-cwf.log && del cs-cwf.log && exit /b 1)

cs-ide.zip:
	@pwsh.exe .github/cli.ps1 > cs-ide.log 2>&1 && del cs-ide.log || (type cs-ide.log && del cs-ide.log && exit /b 1)

.copy-assets: cs-cwf.zip cs-ide.zip
	@pwsh.exe .github/copy-assets.ps1 > copy-assets.log 2>&1 && del copy-assets.log || (type copy-assets.log && del copy-assets.log && exit /b 1)
	@type nul > .copy-assets

copy-assets: .copy-assets

restore:
	@dotnet.exe restore Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > restore.log 2>&1 && del restore.log || (type restore.log && del restore.log && exit /b 1)

# Build only runs if source files or assets are newer than .build-timestamp
.build-timestamp: $(CS_FILES) $(PROJ_FILES) .copy-assets
	@dotnet.exe restore Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > restore.log 2>&1 && del restore.log || (type restore.log && del restore.log && exit /b 1)
	@cd Codescene.VSExtension.VS2022 && MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release > build.log 2>&1 && del build.log || (type build.log && del build.log && exit /b 1)
	@echo Build completed at %date% %time% > .build-timestamp

build: .build-timestamp

test: build
	$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/test.ps1)

# make test1 TEST=GitChangeObserverTests
test1: build
	$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/test1.ps1 -TestName $(TEST))

# Runs tests for changed *Tests.cs files (per Git)
test-mine: build
	$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/test-mine.ps1)

# Formats just the .cs files you've worked on (per Git)
format:
	$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/format-mine.ps1) > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

# Formats all files.
format-all:
	$(call call_cached,$(CACHE_KEY),dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln) > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

format-check:
	$(call call_cached,$(CACHE_KEY),dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln --verify-no-changes) > format-check.log 2>&1 && del format-check.log || (type format.log && del format-check.log && exit /b 1)

.run-analyzers: restore
	@$(call call_cached,$(CACHE_KEY),cd Codescene.VSExtension.VS2022 && MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release -p:RunStyleCopAnalyzers=true)

stylecop: restore
	@$(MAKE) .run-analyzers > analyzers.log 2>&1
	@pwsh.exe -Command "$$warnings = Select-String -Path 'analyzers.log' -Pattern 'warning SA' | ForEach-Object { $$_.Line } | Sort-Object -Unique; if ($$warnings) { $$warnings | ForEach-Object { Write-Host $$_ }; exit 1 } else { exit 0 }"

stylecop-mine: restore
	@$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/check-mine.ps1 -Pattern \"warning SA\")

dotnet-analyzers: restore
	@$(MAKE) .run-analyzers > analyzers.log 2>&1
	@pwsh.exe -Command "$$warnings = Select-String -Path 'analyzers.log' -Pattern 'warning' | Where-Object { $$_.Line -notmatch 'warning SA' } | ForEach-Object { $$_.Line } | Sort-Object -Unique; if ($$warnings) { $$warnings | ForEach-Object { Write-Host $$_ }; exit 1 } else { exit 0 }"

dotnet-analyzers-mine: restore
	@$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/check-mine.ps1 -Pattern \"warning\" -ExcludePattern \"warning SA\")

class-size-mine:
	@$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/check-class-size-mine.ps1)

no-regions-mine:
	@$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/check-no-regions-mine.ps1)

# iter - iterate. Good as a prompt: "iterate to success using `make iter`"
# `format` temporarily removed.
iter: build class-size-mine no-regions-mine dotnet-analyzers-mine stylecop-mine delta test-mine

install-cli:
	@pwsh.exe -Command "Set-ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; Invoke-WebRequest -Uri 'https://downloads.codescene.io/enterprise/cli/install-cs-tool.ps1' -OutFile install-cs-tool.ps1; .\install-cs-tool.ps1; Remove-Item install-cs-tool.ps1"

delta:
	@if not exist "%USERPROFILE%\AppData\Local\Programs\CodeScene\cs.exe" $(MAKE) install-cli
	$(call call_cached,$(CACHE_KEY),pwsh.exe -File .github/delta.ps1) > delta.log 2>&1 && del delta.log || (type delta.log && del delta.log && exit /b 1)
