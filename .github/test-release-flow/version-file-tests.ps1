function New-VersionRepository {
    $root = New-TempDirectory
    $real = Get-VersionFilePaths -RepositoryPath $script:RepositoryRoot
    $copy = Get-VersionFilePaths -RepositoryPath $root
    New-Item -ItemType Directory -Path (Split-Path -Parent $copy.Manifest) | Out-Null
    Copy-Item -LiteralPath $real.Manifest -Destination $copy.Manifest
    Copy-Item -LiteralPath $real.Source -Destination $copy.Source
    return $root
}

function Read-AllText([string]$Path) {
    return (New-Object System.Text.UTF8Encoding($false)).GetString([IO.File]::ReadAllBytes($Path))
}

function Write-AllText([string]$Path, [string]$Text) {
    [IO.File]::WriteAllBytes($Path, (New-Object System.Text.UTF8Encoding($false)).GetBytes($Text))
}

function Get-DifferingLineCount([string]$Left, [string]$Right) {
    $leftLines = $Left -split "`n"
    $rightLines = $Right -split "`n"
    Assert-Equal -Expected $leftLines.Count -Actual $rightLines.Count -Name 'compared texts have the same line count'
    $count = 0
    for ($i = 0; $i -lt $leftLines.Count; $i++) {
        if ($leftLines[$i] -cne $rightLines[$i]) { $count++ }
    }
    return $count
}

function Get-CarriageReturnCount([byte[]]$Bytes) {
    return @($Bytes | Where-Object { $_ -eq 13 }).Count
}

function New-VsixFile([string]$Directory, [string]$Name, [string]$Version) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $path = Join-Path $Directory $Name
    $manifest = "<?xml version=`"1.0`" encoding=`"utf-8`"?><PackageManifest Version=`"2.0.0`" xmlns=`"http://schemas.microsoft.com/developer/vsx-schema/2011`"><Metadata><Identity Id=`"CodeScene.test`" Version=`"$Version`" Language=`"en-US`" Publisher=`"CodeScene`" /></Metadata><Installation><InstallationTarget Id=`"Microsoft.VisualStudio.Community`" Version=`"[17.14, 19.0)`" /></Installation></PackageManifest>"
    $archive = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in @(@('extension.vsixmanifest', $manifest), @('other.txt', 'x'))) {
            $entry = $archive.CreateEntry($item[0])
            $writer = New-Object System.IO.StreamWriter($entry.Open())
            try {
                $writer.Write($item[1])
            }
            finally {
                $writer.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
    return $path
}

function Test-VersionFilePaths {
    $paths = Get-VersionFilePaths -RepositoryPath 'C:\repo'
    Assert-Equal -Expected 'C:\repo\Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.vsixmanifest' -Actual $paths.Manifest -Name 'manifest path'
    Assert-Equal -Expected 'C:\repo\Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\source.extension.cs' -Actual $paths.Source -Name 'source path'
    $real = Get-VersionFilePaths -RepositoryPath $script:RepositoryRoot
    Assert-True -Condition (Test-Path -LiteralPath $real.Manifest) -Name 'the manifest path exists in this repository'
    Assert-True -Condition (Test-Path -LiteralPath $real.Source) -Name 'the source path exists in this repository'
}

function Test-ReleaseVersionFiles {
    $root = New-VersionRepository
    $paths = Get-VersionFilePaths -RepositoryPath $root
    $original = Get-ReleaseVersion -RepositoryPath $root
    Assert-True -Condition ($original -cmatch '^\d+\.\d+\.\d+$') -Name 'the committed manifest version is well formed'
    $manifestBefore = Read-AllText $paths.Manifest
    $sourceBefore = Read-AllText $paths.Source

    Set-ReleaseVersion -RepositoryPath $root -Version '1.2.3'

    Assert-Equal -Expected '1.2.3' -Actual (Get-ReleaseVersion -RepositoryPath $root) -Name 'get returns the rewritten version'
    $manifestAfter = Read-AllText $paths.Manifest
    $sourceAfter = Read-AllText $paths.Source
    Assert-Equal -Expected 1 -Actual (Get-DifferingLineCount $manifestBefore $manifestAfter) -Name 'manifest rewrite changes exactly one line'
    Assert-Equal -Expected 1 -Actual (Get-DifferingLineCount $sourceBefore $sourceAfter) -Name 'source rewrite changes exactly one line'
    Assert-Equal -Expected $manifestBefore -Actual $manifestAfter.Replace("Version=`"1.2.3`" Language", "Version=`"$original`" Language") -Name 'manifest text elsewhere is identical'
    Assert-Equal -Expected $sourceBefore -Actual $sourceAfter.Replace('Version = "1.2.3";', "Version = `"$original`";") -Name 'source text elsewhere is identical'
    Assert-True -Condition ($manifestAfter.Contains('<PackageManifest Version="2.0.0"')) -Name 'PackageManifest version is untouched'
    Assert-True -Condition ($manifestAfter.Contains('<InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[17.14, 19.0)">')) -Name 'InstallationTarget version is untouched'
    Assert-True -Condition ($manifestAfter.Contains('Version="[17.14,)"')) -Name 'Prerequisite version is untouched'
    Assert-True -Condition ($sourceAfter.EndsWith($sourceBefore.Substring($sourceBefore.IndexOf('public const string Author')))) -Name 'source tail including trailing blank lines is preserved'

    Set-ReleaseVersion -RepositoryPath $root -Version $original
    Assert-True -Condition ($manifestBefore -ceq (Read-AllText $paths.Manifest)) -Name 'restoring the version restores the manifest'
    Assert-True -Condition ($sourceBefore -ceq (Read-AllText $paths.Source)) -Name 'restoring the version restores the source'
}

function Test-ReleaseVersionPreservesEncoding {
    $root = New-VersionRepository
    $paths = Get-VersionFilePaths -RepositoryPath $root
    $manifestBytes = [IO.File]::ReadAllBytes($paths.Manifest)
    Assert-True -Condition ($manifestBytes[0] -ne 0xEF) -Name 'the manifest fixture has no BOM'
    $sourceBefore = [IO.File]::ReadAllBytes($paths.Source)
    Assert-True -Condition ((Get-CarriageReturnCount $sourceBefore) -gt 0) -Name 'the source fixture uses CRLF'

    Set-ReleaseVersion -RepositoryPath $root -Version '2.0.0'

    $manifestAfter = [IO.File]::ReadAllBytes($paths.Manifest)
    Assert-True -Condition ($manifestAfter[0] -ne 0xEF) -Name 'no BOM is added to the manifest'
    $sourceAfter = [IO.File]::ReadAllBytes($paths.Source)
    Assert-Equal -Expected (Get-CarriageReturnCount $sourceBefore) -Actual (Get-CarriageReturnCount $sourceAfter) -Name 'CRLF count is preserved'
    Assert-True -Condition ($sourceAfter[0] -ne 0xEF) -Name 'no BOM is added to the source'

    $bomRoot = New-VersionRepository
    $bomPaths = Get-VersionFilePaths -RepositoryPath $bomRoot
    [IO.File]::WriteAllBytes($bomPaths.Manifest, [byte[]](0xEF, 0xBB, 0xBF) + [IO.File]::ReadAllBytes($bomPaths.Manifest))
    Set-ReleaseVersion -RepositoryPath $bomRoot -Version '2.0.0'
    $bomAfter = [IO.File]::ReadAllBytes($bomPaths.Manifest)
    Assert-True -Condition ($bomAfter[0] -eq 0xEF -and $bomAfter[1] -eq 0xBB -and $bomAfter[2] -eq 0xBF) -Name 'an existing BOM is preserved'
    Assert-Equal -Expected '2.0.0' -Actual (Get-ReleaseVersion -RepositoryPath $bomRoot) -Name 'a manifest with a BOM is still read'
}

function Test-ReleaseVersionFailures {
    $duplicated = New-VersionRepository
    $duplicatedPaths = Get-VersionFilePaths -RepositoryPath $duplicated
    $source = Read-AllText $duplicatedPaths.Source
    $line = ([regex]::Match($source, 'public const string Version = "[^"]*";\r?\n')).Value
    Write-AllText $duplicatedPaths.Source $source.Replace($line, $line + $line)
    Assert-Throws -Action { Set-ReleaseVersion -RepositoryPath $duplicated -Version '1.2.3' } -MessagePattern 'expected exactly one version' -Name 'a duplicated source version throws'

    $noSource = New-VersionRepository
    $noSourcePaths = Get-VersionFilePaths -RepositoryPath $noSource
    Write-AllText $noSourcePaths.Source ((Read-AllText $noSourcePaths.Source) -replace 'public const string Version = "[^"]*";', '')
    Assert-Throws -Action { Set-ReleaseVersion -RepositoryPath $noSource -Version '1.2.3' } -MessagePattern 'expected exactly one version' -Name 'a missing source version throws'

    $noIdentity = New-VersionRepository
    $noIdentityPaths = Get-VersionFilePaths -RepositoryPath $noIdentity
    Write-AllText $noIdentityPaths.Manifest ((Read-AllText $noIdentityPaths.Manifest) -replace '(<Identity\b[^>]*?)\sVersion="[^"]*"', '$1')
    Assert-Throws -Action { Get-ReleaseVersion -RepositoryPath $noIdentity } -MessagePattern 'No Identity version' -Name 'a manifest without an Identity version throws on read'
    Assert-Throws -Action { Set-ReleaseVersion -RepositoryPath $noIdentity -Version '1.2.3' } -MessagePattern 'expected exactly one version' -Name 'a manifest without an Identity version throws on write'

    $emptyIdentity = New-VersionRepository
    $emptyPaths = Get-VersionFilePaths -RepositoryPath $emptyIdentity
    Write-AllText $emptyPaths.Manifest ((Read-AllText $emptyPaths.Manifest) -replace '(<Identity\b[^>]*?\sVersion=")[^"]*"', '$1"')
    Assert-Throws -Action { Get-ReleaseVersion -RepositoryPath $emptyIdentity } -MessagePattern 'No Identity version' -Name 'an empty Identity version throws on read'

    $malformed = New-VersionRepository
    $malformedPaths = Get-VersionFilePaths -RepositoryPath $malformed
    $before = [IO.File]::ReadAllBytes($malformedPaths.Manifest)
    Assert-Throws -Action { Set-ReleaseVersion -RepositoryPath $malformed -Version 'v1.2.3' } -MessagePattern 'Malformed version' -Name 'a malformed version is rejected'
    Assert-Throws -Action { Set-ReleaseVersion -RepositoryPath $malformed -Version '1.2.65535' } -MessagePattern 'Malformed version' -Name 'an over-limit version is rejected'
    Assert-Equal -Expected ($before -join ',') -Actual ([IO.File]::ReadAllBytes($malformedPaths.Manifest) -join ',') -Name 'a rejected version leaves the manifest untouched'
}

function Test-VsixVerification {
    $directory = New-TempDirectory
    $vsix = New-VsixFile $directory 'a.vsix' '0.9.2'
    Assert-Equal -Expected '0.9.2' -Actual (Get-VsixManifestVersion -Path $vsix) -Name 'vsix manifest version is read'
    Assert-VsixVersion -Path $vsix -Version '0.9.2'
    $script:Passed++
    Assert-Throws -Action { Assert-VsixVersion -Path $vsix -Version '0.9.3' } -MessagePattern 'mismatch.*0\.9\.2.*0\.9\.3' -Name 'a differing vsix version throws'

    $plain = Join-Path $directory 'plain.zip'
    $archive = [System.IO.Compression.ZipFile]::Open($plain, [System.IO.Compression.ZipArchiveMode]::Create)
    $archive.CreateEntry('readme.txt') | Out-Null
    $archive.Dispose()
    Assert-Throws -Action { Get-VsixManifestVersion -Path $plain } -MessagePattern 'does not contain extension\.vsixmanifest' -Name 'an archive without a manifest throws'
}

function Test-FindSingleVsix {
    $empty = New-TempDirectory
    Assert-Throws -Action { Find-SingleVsix -Root $empty } -MessagePattern 'No \.vsix' -Name 'no vsix throws'

    $one = New-TempDirectory
    New-Item -ItemType Directory -Path (Join-Path $one 'nested\deeper') | Out-Null
    $file = Join-Path $one 'nested\deeper\x.vsix'
    Set-Content -LiteralPath $file -Value 'x'
    Set-Content -LiteralPath (Join-Path $one 'x.vsix.txt') -Value 'x'
    Set-Content -LiteralPath (Join-Path $one 'notes.txt') -Value 'x'
    Assert-Equal -Expected $file -Actual (Find-SingleVsix -Root $one) -Name 'a single nested vsix is returned'

    $two = New-TempDirectory
    Set-Content -LiteralPath (Join-Path $two 'a.vsix') -Value 'x'
    New-Item -ItemType Directory -Path (Join-Path $two 'sub') | Out-Null
    Set-Content -LiteralPath (Join-Path $two 'sub\b.vsix') -Value 'x'
    Assert-Throws -Action { Find-SingleVsix -Root $two } -MessagePattern 'exactly one \.vsix.*found 2' -Name 'two vsix files throw'
}

function Test-BuiltVsixIntegration {
    $repository = $script:RepositoryRoot
    $built = Join-Path $repository 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\bin\Release\Codescene.VSExtension.VS2022.vsix'
    if (-not (Test-Path -LiteralPath $built)) {
        Write-Host 'skipping built vsix check: no local Release build'
        return
    }
    $embedded = Get-VsixManifestVersion -Path $built
    Assert-True -Condition ($embedded -cmatch '^\d+\.\d+\.\d+$') -Name 'the built vsix embeds a well formed version'
    $committed = Get-ReleaseVersion -RepositoryPath $repository
    if ($embedded -cne $committed) {
        Write-Host "built vsix embeds $embedded but the committed version is $committed (stale local build)"
        return
    }
    Assert-VsixVersion -Path $built -Version $committed
    $script:Passed++
}
