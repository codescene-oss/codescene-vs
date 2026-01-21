$csPath = "$env:USERPROFILE\AppData\Local\Programs\CodeScene\cs.exe"
& $csPath delta main --git-hook

if ($LASTEXITCODE -ne 0) {
    exit 1
}
