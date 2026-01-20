SHELL := cmd.exe

.PHONY: test test1 test-mine copy-assets restore format format-all format-check stylecop stylecop-mine dotnet-analyzers dotnet-analyzers-mine

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

FIND_TESTS = Get-ChildItem -Recurse -Filter '*Tests.dll' -Path 'Codescene.VSExtension.VS2022' | Where-Object { $$_.FullName -match 'bin\\Release' } | Select-Object -ExpandProperty FullName
GET_GIT_STATUS = git status --short --porcelain | ForEach-Object { $$_.Substring(3) }
GET_GIT_DIFF = git diff --name-only main...HEAD
COMBINE_CHANGED_FILES = @($$status) + @($$diff) | Where-Object { $$_ -ne $$null -and $$_ -ne '' } | Select-Object -Unique
FILTER_CS_FILES = Where-Object { $$_ -match '\\.cs$$' }
FILTER_TEST_FILES = Where-Object { $$_ -match 'Tests\.cs$$' }
EXTRACT_TEST_NAMES = ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($$_) }
JOIN_TEST_NAMES = if ($$testNames -is [array]) { $$testNames -join ',' } else { $$testNames }
RUN_TESTS_BY_NAME = $$testDlls = $(FIND_TESTS); $$testNameStr = $(JOIN_TEST_NAMES); vstest.console.exe $$testDlls /Tests:$$testNameStr /logger:trx
EXTRACT_STYLECOP_WARNINGS = Select-String -Path 'stylecop.log' -Pattern 'warning SA' | ForEach-Object { $$_.Line } | Sort-Object -Unique
LOG_SUCCESS = powershell.exe -Command "Get-Content test.log -Tail 4" && del test.log
LOG_FAILURE = type test.log && del test.log && exit /b 1
STYLECOP_OUTPUT = powershell.exe -Command "$(EXTRACT_STYLECOP_WARNINGS)" && del stylecop.log
STYLECOP_FAILURE = type stylecop.log && del stylecop.log && exit /b 1
EXTRACT_DOTNET_WARNINGS = Select-String -Path 'dotnet-analyzers.log' -Pattern 'warning CA' | ForEach-Object { $$_.Line } | Sort-Object -Unique
DOTNET_ANALYZERS_OUTPUT = powershell.exe -Command "$(EXTRACT_DOTNET_WARNINGS)" && del dotnet-analyzers.log
DOTNET_ANALYZERS_FAILURE = type dotnet-analyzers.log && del dotnet-analyzers.log && exit /b 1

test: build
	@powershell.exe -Command "$$files = $(FIND_TESTS); vstest.console.exe $$files /logger:trx" > test.log 2>&1 && $(LOG_SUCCESS) || ($(LOG_FAILURE))

# make test1 TEST=GitChangeObserverTests
test1: build
	@powershell.exe -Command "$$files = $(FIND_TESTS); vstest.console.exe $$files /Tests:$(TEST) /logger:trx" > test.log 2>&1 && $(LOG_SUCCESS) || ($(LOG_FAILURE))

# Runs tests for changed *Tests.cs files (per Git)
test-mine: build
	@powershell.exe -Command " \
		$$status = $(GET_GIT_STATUS); \
		$$diff = $(GET_GIT_DIFF); \
		$$allFiles = $(COMBINE_CHANGED_FILES); \
		$$csFiles = $$allFiles | $(FILTER_CS_FILES); \
		$$testFiles = $$csFiles | $(FILTER_TEST_FILES); \
		if ($$testFiles) { \
			$$testNames = $$testFiles | $(EXTRACT_TEST_NAMES); \
			$(RUN_TESTS_BY_NAME) \
		} \
	" > test.log 2>&1 && $(LOG_SUCCESS) || ($(LOG_FAILURE))

# Formats just the .cs files you've worked on (per Git)
format:
	@powershell.exe -Command " \
		$$status = $(GET_GIT_STATUS); \
		$$diff = $(GET_GIT_DIFF); \
		$$allFiles = $(COMBINE_CHANGED_FILES); \
		$$files = $$allFiles | $(FILTER_CS_FILES); \
		if ($$files) { \
			dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln --include ($$files -join ' ') \
		} else { \
			Write-Host 'No C# files to format' \
		} \
	" > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

# Formats all files.
format-all:
	@dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln > format.log 2>&1 && del format.log || (type format.log && del format.log && exit /b 1)

format-check:
	@dotnet.exe format Codescene.VSExtension.VS2022/Codescene.VSExtension.sln --verify-no-changes > format-check.log 2>&1 && del format-check.log || (type format-check.log && del format-check.log && exit /b 1)

stylecop: restore
	@cd Codescene.VSExtension.VS2022 && MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release -p:RunStyleCopAnalyzers=true > stylecop.log 2>&1 && $(STYLECOP_OUTPUT) || ($(STYLECOP_FAILURE))

stylecop-mine: restore
	@powershell.exe -Command " \
		$$status = $(GET_GIT_STATUS); \
		$$diff = $(GET_GIT_DIFF); \
		$$allFiles = $(COMBINE_CHANGED_FILES); \
		$$files = $$allFiles | $(FILTER_CS_FILES); \
		if ($$files) { \
			cd Codescene.VSExtension.VS2022; \
			dotnet.exe format analyzers Codescene.VSExtension.sln --severity info --no-restore --include ($$files -join ' ') \
		} else { \
			Write-Host 'No changed C# files to check' -ForegroundColor Yellow \
		} \
	" > stylecop.log 2>&1 && $(STYLECOP_OUTPUT) || ($(STYLECOP_FAILURE))

dotnet-analyzers: restore
	@cd Codescene.VSExtension.VS2022 && MSBuild.exe Codescene.VSExtension.sln -p:Configuration=Release > dotnet-analyzers.log 2>&1 && $(DOTNET_ANALYZERS_OUTPUT) || ($(DOTNET_ANALYZERS_FAILURE))

dotnet-analyzers-mine: restore
	@powershell.exe -Command " \
		$$status = $(GET_GIT_STATUS); \
		$$diff = $(GET_GIT_DIFF); \
		$$allFiles = $(COMBINE_CHANGED_FILES); \
		$$files = $$allFiles | $(FILTER_CS_FILES); \
		if ($$files) { \
			cd Codescene.VSExtension.VS2022; \
			dotnet.exe format analyzers Codescene.VSExtension.sln --severity info --no-restore --include ($$files -join ' ') \
		} else { \
			Write-Host 'No changed C# files to check' -ForegroundColor Yellow \
		} \
	" > dotnet-analyzers.log 2>&1 && $(DOTNET_ANALYZERS_OUTPUT) || ($(DOTNET_ANALYZERS_FAILURE))

# iter - iterate. Good as a promopt: "iterate to success using `make iter`"
iter: format dotnet-analyzers-mine stylecop-mine test-mine

