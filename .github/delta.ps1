$csPath = "$env:USERPROFILE\AppData\Local\Programs\CodeScene\cs.exe"
& $csPath delta main --error-on-warnings

if ($LASTEXITCODE -ne 0) {
    exit 1
}
