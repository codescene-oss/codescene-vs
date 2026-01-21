# sha.mk - Cache key computation
#
# Computes a cache key based on:
#   1. SHA of git HEAD (first 6 chars)
#   2. SHA of concatenated filenames from git status (first 6 chars)
#
# Usage:
#   CACHE_KEY := $(call get_cache_key)
#
# The cache key format is: <head-sha-6><files-sha-6>

define get_cache_key
$(shell powershell.exe -NoProfile -Command " \
	$$headSha = (git rev-parse --short=6 HEAD).Trim(); \
	$$excludedFiles = @('Makefile'); \
	$$files = git status --untracked-files=all --short --porcelain | ForEach-Object { $$_.Substring(3).Trim() } | Where-Object { (Test-Path $$_) -and ((Get-Item $$_).Length -le 204800) -and ($$excludedFiles -notcontains $$_) } | Sort-Object; \
	$$concat = ($$files -join ''); \
	$$bytes = [Text.Encoding]::UTF8.GetBytes($$concat); \
	$$stream = [IO.MemoryStream]::new($$bytes); \
	$$filesSha = (Get-FileHash -InputStream $$stream -Algorithm SHA256).Hash.Substring(0,6); \
	$$stream.Dispose(); \
	Write-Output \"$$headSha$$filesSha\"")
endef
