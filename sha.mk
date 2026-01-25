# sha.mk - Cache key computation
#
# Computes a cache key based on the content of all .cs and .csproj files
# (both tracked and untracked). Uses git object hashes for efficiency.
#
# Usage:
#   CACHE_KEY := $(call get_cache_key)
#
# The cache key format is: <files-sha-12> (12 characters)

define get_cache_key
$(shell powershell.exe -NoProfile -ExecutionPolicy Bypass -File .github/get-cache-key.ps1)
endef
