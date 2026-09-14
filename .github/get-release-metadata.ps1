param(
    [string]$RepositoryPath = (Join-Path $PSScriptRoot ".."),
    [string]$Ref = $env:GITHUB_REF,
    [string]$Tag = $env:GITHUB_REF_NAME,
    [string]$OutputPath = $env:GITHUB_OUTPUT
)

$ErrorActionPreference = "Stop"

if (-not $OutputPath) {
    throw "An output path is required."
}

function Write-OutputValue {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [AllowEmptyString()]
        [string]$Value
    )

    Add-Content -Path $OutputPath -Value "$Name=$Value" -Encoding utf8
}

if (-not $Ref.StartsWith("refs/tags/", [StringComparison]::Ordinal)) {
    Write-OutputValue -Name "is_release" -Value "false"
    exit 0
}

Import-Module (Join-Path $PSScriptRoot "release-version.psm1") -Force
$metadata = Get-ReleaseMetadata -RepositoryPath $RepositoryPath -Tag $Tag

Write-OutputValue -Name "is_release" -Value "true"
Write-OutputValue -Name "is_test" -Value $metadata.IsTest.ToString().ToLowerInvariant()
Write-OutputValue -Name "tag" -Value $metadata.Tag
Write-OutputValue -Name "package_version" -Value $metadata.PackageVersion
Write-OutputValue -Name "notes_start_tag" -Value $metadata.NotesStartTag
Write-OutputValue -Name "latest_stable_tag" -Value $metadata.LatestStableTag
