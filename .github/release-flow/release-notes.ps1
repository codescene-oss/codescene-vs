function Format-TestReleaseNotes {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Generated,
        [Parameter(Mandatory)]
        [string]$Sha,
        [Parameter(Mandatory)]
        [string]$ServerUrl,
        [Parameter(Mandatory)]
        [string]$Repository
    )

    $short = $Sha.Substring(0, [Math]::Min(7, $Sha.Length))
    $banner = "Test build, not published to the Visual Studio Marketplace. Commit: [$short]($ServerUrl/$Repository/commit/$Sha)"
    return "$banner`n`n$Generated"
}

function Get-ReleaseNotesBody {
    param(
        [Parameter(Mandatory)]
        [string]$Kind,
        [AllowNull()][AllowEmptyString()]
        [string]$Generated,
        [string]$Sha,
        [string]$ServerUrl,
        [string]$Repository
    )

    if ($Kind -ceq 'stable') { return $Generated }
    if ($Kind -ceq 'test') {
        return Format-TestReleaseNotes -Generated $Generated -Sha $Sha -ServerUrl $ServerUrl -Repository $Repository
    }
    throw "Unknown tag kind '$Kind'. Expected stable or test."
}

function Write-ReleaseNotesFile {
    param(
        [Parameter(Mandatory)]
        [string]$Tag,
        [AllowNull()][AllowEmptyString()]
        [string]$Body
    )

    $directory = $env:RUNNER_TEMP
    if ([string]::IsNullOrWhiteSpace($directory)) {
        $directory = [IO.Path]::GetTempPath()
    }
    $path = Join-Path $directory "release-notes-$Tag.md"
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($path, "$Body", $encoding)
    return $path
}
