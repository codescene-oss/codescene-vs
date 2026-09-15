$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "release-version.psm1") -Force

$script:Passed = 0

function Assert-Equal {
    param(
        [Parameter(Mandatory)]
        $Expected,
        [Parameter(Mandatory)]
        $Actual,
        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($Expected -ne $Actual) {
        throw "$Name failed. Expected '$Expected', got '$Actual'."
    }

    $script:Passed++
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,
        [Parameter(Mandatory)]
        [string]$MessagePattern,
        [Parameter(Mandatory)]
        [string]$Name
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "$Name failed. Expected error matching '$MessagePattern', got '$($_.Exception.Message)'."
        }

        $script:Passed++
        return
    }

    throw "$Name failed. Expected an exception."
}

function Assert-Match {
    param(
        [Parameter(Mandatory)]
        [string]$Value,
        [Parameter(Mandatory)]
        [string]$Pattern,
        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($Value -notmatch $Pattern) {
        throw "$Name failed. Expected '$Value' to match '$Pattern'."
    }

    $script:Passed++
}

function New-PackageMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )

    $repositoryPath = Join-Path ([System.IO.Path]::GetTempPath()) ("codescene-release-version-" + [guid]::NewGuid())
    $projectPath = Join-Path $repositoryPath "Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022"
    New-Item -ItemType Directory -Path $projectPath -Force | Out-Null

    @"
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Id="CodeScene" Version="$Version" Language="en-US" Publisher="CodeScene" />
  </Metadata>
</PackageManifest>
"@ | Set-Content (Join-Path $projectPath "source.extension.vsixmanifest") -Encoding utf8

    @"
namespace Codescene.VSExtension.VS2022
{
    internal sealed partial class Vsix
    {
        public const string Version = "$Version";
    }
}
"@ | Set-Content (Join-Path $projectPath "source.extension.cs") -Encoding utf8

    return $repositoryPath
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    $output = & git -C $RepositoryPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function New-GitRepository {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [string]$SourceVersion = $Version
    )

    $repositoryPath = New-PackageMetadata -Version $Version
    $sourcePath = Join-Path $repositoryPath "Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.cs"
    if ($SourceVersion -ne $Version) {
        (Get-Content $sourcePath -Raw).Replace(
            "public const string Version = `"$Version`";",
            "public const string Version = `"$SourceVersion`";"
        ) | Set-Content $sourcePath -Encoding utf8
    }

    Invoke-Git $repositoryPath init | Out-Null
    Invoke-Git $repositoryPath config user.email "test@example.com" | Out-Null
    Invoke-Git $repositoryPath config user.name "Test User" | Out-Null
    Invoke-Git $repositoryPath add "." | Out-Null
    Invoke-Git $repositoryPath commit -m "Initial commit" | Out-Null
    return $repositoryPath
}

function Invoke-TestReleaseScript {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [string]$Bump
    )

    $arguments = @(
        "-NoProfile",
        "-File",
        (Join-Path $PSScriptRoot "..\create-test-release.ps1"),
        "-RepositoryPath",
        $RepositoryPath
    )
    if ($Bump) {
        $arguments += @("-Bump", $Bump)
    }

    $output = & pwsh.exe @arguments 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output -join [Environment]::NewLine)
    }
}

function Add-Commit {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Name
    )

    Set-Content (Join-Path $RepositoryPath "$Name.txt") $Name
    Invoke-Git $RepositoryPath add "." | Out-Null
    Invoke-Git $RepositoryPath commit -m $Name | Out-Null
}

function Invoke-MetadataScript {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Ref,
        [string]$Tag
    )

    $outputPath = Join-Path ([System.IO.Path]::GetTempPath()) ("codescene-release-output-" + [guid]::NewGuid())
    $arguments = @(
        "-NoProfile",
        "-File",
        (Join-Path $PSScriptRoot "get-release-metadata.ps1"),
        "-RepositoryPath",
        $RepositoryPath,
        "-Ref",
        $Ref,
        "-OutputPath",
        $outputPath
    )
    if ($Tag) {
        $arguments += @("-Tag", $Tag)
    }

    $output = & pwsh.exe @arguments 2>&1
    $values = if (Test-Path $outputPath) {
        ConvertFrom-StringData (Get-Content $outputPath -Raw)
    }
    else {
        @{}
    }
    Remove-Item $outputPath -Force -ErrorAction SilentlyContinue

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($output -join [Environment]::NewLine)
        Values = $values
    }
}

$temporaryPaths = [System.Collections.Generic.List[string]]::new()

try {
    Assert-Equal "1.2.4" (Get-NextStableVersion -Version "1.2.3" -Bump "patch") "Patch bump"
    Assert-Equal "1.3.0" (Get-NextStableVersion -Version "1.2.3" -Bump "minor") "Minor bump"
    Assert-Equal "2.0.0" (Get-NextStableVersion -Version "1.2.3" -Bump "major") "Major bump"
    Assert-Throws { Get-NextStableVersion -Version "1.2" -Bump "patch" } "Expected version in X.Y.Z format" "Malformed version"
    Assert-Throws { Get-NextStableVersion -Version "1.2.3" -Bump "banana" } "Expected patch, minor, or major" "Unsupported bump"
    Assert-Throws { Get-NextStableVersion -Version "65534.0.0" -Bump "major" } "65534" "Version component overflow"
    Assert-Throws {
        Get-TestReleaseMetadata -StableVersion "1.2.3" -Bump "patch" -Revision 65535
    } "65534" "Revision component overflow"

    $metadata = Get-TestReleaseMetadata -StableVersion "1.2.3" -Bump "minor" -Revision 42
    Assert-Equal "1.3.0" $metadata.BaseVersion "Test base version"
    Assert-Equal "1.3.0.42" $metadata.PackageVersion "Test package version"
    Assert-Equal "v1.3.0-test.42" $metadata.Tag "Test tag"

    $repositoryPath = New-PackageMetadata -Version "1.2.3"
    $temporaryPaths.Add($repositoryPath)
    $versions = Get-PackageVersions -RepositoryPath $repositoryPath
    Assert-Equal "1.2.3" $versions.ManifestVersion "Manifest version read"
    Assert-Equal "1.2.3" $versions.SourceVersion "Source version read"

    Set-PackageVersion -RepositoryPath $repositoryPath -Version "1.3.0.42"
    $updatedVersions = Get-PackageVersions -RepositoryPath $repositoryPath
    Assert-Equal "1.3.0.42" $updatedVersions.ManifestVersion "Manifest version update"
    Assert-Equal "1.3.0.42" $updatedVersions.SourceVersion "Source version update"

    $patchRepository = New-GitRepository -Version "1.2.3"
    $temporaryPaths.Add($patchRepository)
    $patchRevision = [int](Invoke-Git $patchRepository rev-list --count HEAD)
    $patchResult = Invoke-TestReleaseScript -RepositoryPath $patchRepository
    Assert-Equal 0 $patchResult.ExitCode "Default patch command"
    Assert-Match $patchResult.Output "git push origin v1\.2\.4-test\.$patchRevision" "Push command"
    Assert-Equal "v1.2.4-test.$patchRevision" (Invoke-Git $patchRepository tag --list) "Patch test tag"
    Assert-Equal "tag" (Invoke-Git $patchRepository cat-file -t "v1.2.4-test.$patchRevision") "Annotated tag"
    Assert-Equal "" (Invoke-Git $patchRepository status --porcelain) "Unchanged package metadata"

    $minorRepository = New-GitRepository -Version "1.2.3"
    $temporaryPaths.Add($minorRepository)
    $minorRevision = [int](Invoke-Git $minorRepository rev-list --count HEAD)
    $minorResult = Invoke-TestReleaseScript -RepositoryPath $minorRepository -Bump "minor"
    Assert-Equal 0 $minorResult.ExitCode "Minor command"
    Assert-Equal "v1.3.0-test.$minorRevision" (Invoke-Git $minorRepository tag --list) "Minor test tag"

    $dirtyRepository = New-GitRepository -Version "1.2.3"
    $temporaryPaths.Add($dirtyRepository)
    Set-Content (Join-Path $dirtyRepository "dirty.txt") "dirty"
    $dirtyResult = Invoke-TestReleaseScript -RepositoryPath $dirtyRepository
    Assert-Equal 1 $dirtyResult.ExitCode "Dirty worktree rejection"
    Assert-Match $dirtyResult.Output "clean Git worktree" "Dirty worktree message"
    Assert-Equal "" (Invoke-Git $dirtyRepository tag --list) "No dirty worktree tag"

    $duplicateResult = Invoke-TestReleaseScript -RepositoryPath $patchRepository
    Assert-Equal 1 $duplicateResult.ExitCode "Duplicate tag rejection"
    Assert-Match $duplicateResult.Output "Tag already exists" "Duplicate tag message"

    $mismatchRepository = New-GitRepository -Version "1.2.3" -SourceVersion "1.2.4"
    $temporaryPaths.Add($mismatchRepository)
    $mismatchResult = Invoke-TestReleaseScript -RepositoryPath $mismatchRepository
    Assert-Equal 1 $mismatchResult.ExitCode "Mismatched metadata rejection"
    Assert-Match $mismatchResult.Output "must match" "Mismatched metadata message"

    $historyRepository = New-GitRepository -Version "1.2.2"
    $temporaryPaths.Add($historyRepository)
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", "v1.2.2", "-m", "Release v1.2.2") | Out-Null
    Set-PackageVersion -RepositoryPath $historyRepository -Version "1.2.3"
    Invoke-Git $historyRepository add "." | Out-Null
    Invoke-Git $historyRepository commit -m "Release 1.2.3" | Out-Null
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", "v1.2.3", "-m", "Release v1.2.3") | Out-Null

    $stableMetadata = Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag "v1.2.3"
    Assert-Equal $false $stableMetadata.IsTest "Stable release type"
    Assert-Equal "1.2.3" $stableMetadata.PackageVersion "Stable package version"
    Assert-Equal "v1.2.2" $stableMetadata.NotesStartTag "Stable notes baseline"

    Add-Commit -RepositoryPath $historyRepository -Name "first-test"
    $firstRevision = [int](Invoke-Git $historyRepository rev-list --count HEAD)
    $firstTag = "v1.2.4-test.$firstRevision"
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", $firstTag, "-m", "First test") | Out-Null
    $firstMetadata = Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag $firstTag
    Assert-Equal $true $firstMetadata.IsTest "Test release type"
    Assert-Equal "1.2.4.$firstRevision" $firstMetadata.PackageVersion "Test package version"
    Assert-Equal "v1.2.3" $firstMetadata.NotesStartTag "First test notes baseline"
    Assert-Equal "v1.2.3" $firstMetadata.LatestStableTag "Latest stable tag"

    Add-Commit -RepositoryPath $historyRepository -Name "second-test"
    $secondRevision = [int](Invoke-Git $historyRepository rev-list --count HEAD)
    $secondTag = "v1.2.4-test.$secondRevision"
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", $secondTag, "-m", "Second test") | Out-Null
    $secondMetadata = Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag $secondTag
    Assert-Equal $firstTag $secondMetadata.NotesStartTag "Subsequent test notes baseline"

    Assert-Throws {
        Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag "release-1.2.3"
    } "Expected release tag" "Unknown release tag"

    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", "v1.2.4", "-m", "Mismatched stable") | Out-Null
    Assert-Throws {
        Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag "v1.2.4"
    } "must match committed version" "Mismatched stable tag"

    $invalidBaseTag = "v1.4.0-test.$secondRevision"
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", $invalidBaseTag, "-m", "Invalid base") | Out-Null
    Assert-Throws {
        Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag $invalidBaseTag
    } "next patch, minor, or major" "Invalid test base"

    $invalidRevisionTag = "v1.2.4-test.$($secondRevision + 1)"
    Invoke-Git -RepositoryPath $historyRepository -Arguments @("tag", "-a", $invalidRevisionTag, "-m", "Invalid revision") | Out-Null
    Assert-Throws {
        Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag $invalidRevisionTag
    } "commit count" "Invalid test revision"

    Add-Commit -RepositoryPath $historyRepository -Name "lightweight-test"
    $lightweightRevision = [int](Invoke-Git $historyRepository rev-list --count HEAD)
    $lightweightTag = "v1.3.0-test.$lightweightRevision"
    Invoke-Git $historyRepository tag $lightweightTag | Out-Null
    Assert-Throws {
        Get-ReleaseMetadata -RepositoryPath $historyRepository -Tag $lightweightTag
    } "annotated" "Lightweight tag rejection"

    $branchOutput = Invoke-MetadataScript -RepositoryPath $historyRepository -Ref "refs/heads/main"
    Assert-Equal 0 $branchOutput.ExitCode "Branch metadata command"
    Assert-Equal "false" $branchOutput.Values.is_release "Branch release output"

    $tagOutput = Invoke-MetadataScript -RepositoryPath $historyRepository -Ref "refs/tags/$secondTag" -Tag $secondTag
    Assert-Equal 0 $tagOutput.ExitCode "Tag metadata command"
    Assert-Equal "true" $tagOutput.Values.is_release "Tag release output"
    Assert-Equal "true" $tagOutput.Values.is_test "Tag test output"
    Assert-Equal "1.2.4.$secondRevision" $tagOutput.Values.package_version "Tag package output"
    Assert-Equal $firstTag $tagOutput.Values.notes_start_tag "Tag baseline output"

    $workflow = Get-Content (Join-Path $PSScriptRoot "workflows\github-release.yml") -Raw
    Assert-Match $workflow '& gh release view \$tag --json id' "Existing release detection"
    Assert-Match $workflow '& gh release upload \$tag \$files --clobber' "Existing release asset replacement"
}
finally {
    foreach ($path in $temporaryPaths) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "$script:Passed release version tests passed."
