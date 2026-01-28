# cache.mk - Caching macro for arbitrary program execution
#
# Usage:
#   $(call call_cached,CACHE_KEY,COMMAND)
#
# Parameters:
#   CACHE_KEY - Unique identifier for caching this specific command execution
#   COMMAND   - The command to execute (with arguments)
#
# Example:
#   my-target:
#   	$(call call_cached,my-expensive-op,dotnet.exe restore MyProject.sln)
#
# Cache structure:
#   .cache/CACHE_KEY/<SHA256-of-command>/stdout
#   .cache/CACHE_KEY/<SHA256-of-command>/stderr
#   .cache/CACHE_KEY/<SHA256-of-command>/exitcode
#
# The macro will:
#   - Compute SHA256 hash of the full command string (including arguments)
#   - Create 3-level directory structure: CACHE_DIR/CACHE_KEY/<hash>/
#   - Cache stdout, stderr, and exit code as separate files
#   - On first run: execute command and save outputs
#   - On subsequent runs: replay cached outputs and exit with cached exit code
#   - Properly propagate exit codes (both success and failure)
#
# To invalidate cache:
#   make clean-cache

CACHE_DIR := .cache

$(CACHE_DIR):
	@if not exist $(CACHE_DIR) mkdir $(CACHE_DIR)

define call_cached
	@pwsh.exe -Command " \
		$$cacheKey = '$(1)'; \
		$$command = '$(2)'; \
		$$cacheDir = '$(CACHE_DIR)'; \
		$$stringAsStream = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($$command)); \
		$$commandHash = (Get-FileHash -InputStream $$stringAsStream -Algorithm SHA256).Hash.ToLower(); \
		$$stringAsStream.Dispose(); \
		$$cachePath = Join-Path $$cacheDir (Join-Path $$cacheKey $$commandHash); \
		$$stdoutFile = Join-Path $$cachePath 'stdout'; \
		$$stderrFile = Join-Path $$cachePath 'stderr'; \
		$$exitcodeFile = Join-Path $$cachePath 'exitcode'; \
		if (Test-Path $$stdoutFile) { \
			if (Test-Path $$stdoutFile) { Get-Content $$stdoutFile -Raw | Write-Host -NoNewline }; \
			if ((Test-Path $$stderrFile) -and (Get-Item $$stderrFile).Length -gt 0) { \
				$$stderrContent = Get-Content $$stderrFile -Raw; \
				[Console]::Error.Write($$stderrContent) \
			}; \
			$$exitCode = [int](Get-Content $$exitcodeFile); \
			exit $$exitCode \
		} else { \
			if (-not (Test-Path $$cachePath)) { New-Item -ItemType Directory -Path $$cachePath -Force | Out-Null }; \
			try { \
				$$pinfo = New-Object System.Diagnostics.ProcessStartInfo; \
				$$pinfo.FileName = 'cmd.exe'; \
				$$pinfo.Arguments = '/c ' + $$command; \
				$$pinfo.RedirectStandardOutput = $$true; \
				$$pinfo.RedirectStandardError = $$true; \
				$$pinfo.UseShellExecute = $$false; \
				$$pinfo.CreateNoWindow = $$true; \
				$$p = New-Object System.Diagnostics.Process; \
				$$p.StartInfo = $$pinfo; \
				$$p.Start() | Out-Null; \
				$$stdout = $$p.StandardOutput.ReadToEnd(); \
				$$stderr = $$p.StandardError.ReadToEnd(); \
				$$p.WaitForExit(); \
				$$exitCode = $$p.ExitCode; \
				Set-Content -Path $$stdoutFile -Value $$stdout -NoNewline; \
				Set-Content -Path $$stderrFile -Value $$stderr -NoNewline; \
				Set-Content -Path $$exitcodeFile -Value $$exitCode; \
				Write-Host -NoNewline $$stdout; \
				if ($$stderr) { [Console]::Error.Write($$stderr) }; \
				exit $$exitCode \
			} catch { \
				[Console]::Error.WriteLine($$_.Exception.Message); \
				exit 1 \
			} \
		} \
	"
endef

.PHONY: clean-cache
clean-cache:
	@if exist $(CACHE_DIR) rmdir /s /q $(CACHE_DIR)
