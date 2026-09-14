param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",
    [string]$RepositoryPath = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

try {
    Import-Module (Join-Path $PSScriptRoot ".github\release-version.psm1") -Force
    $metadata = New-TestReleaseTag -RepositoryPath $RepositoryPath -Bump $Bump
    Write-Host "Created annotated test tag $($metadata.Tag) with VSIX version $($metadata.PackageVersion)."
    Write-Host "Push when ready: git push origin $($metadata.Tag)"
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
