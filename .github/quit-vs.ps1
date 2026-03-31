$ErrorActionPreference = "Stop"

# Find all Visual Studio processes
$vsProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue

if (-not $vsProcesses) {
    exit 0
}

$count = @($vsProcesses).Count
$hasExperimental = $vsProcesses | Where-Object { $_.MainWindowTitle -like "*experimental instance*" }

# If 2+ instances and one is experimental: force-kill all
if ($count -ge 2 -and $hasExperimental) {
    foreach ($proc in $vsProcesses) {
        $proc.Kill()
    }
    exit 0
}

# If exactly 1 instance and it's not experimental: gracefully quit with 12s timeout
if ($count -eq 1 -and -not $hasExperimental) {
    $vsProcesses[0].CloseMainWindow() | Out-Null

    $timeout = 12
    $elapsed = 0
    $checkInterval = 0.5

    while ($elapsed -lt $timeout) {
        Start-Sleep -Seconds $checkInterval
        $elapsed += $checkInterval

        $remainingProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue
        if (-not $remainingProcesses) {
            exit 0
        }
    }

    $remainingProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue
    if ($remainingProcesses) {
        Write-Error "Visual Studio instances still running after $timeout seconds. Please close them manually."
        exit 1
    }
}

# Otherwise do nothing (e.g., single experimental instance)
exit 0
