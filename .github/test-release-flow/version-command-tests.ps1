function Test-SetVersionIntegration {
    $repository = $script:RepositoryRoot
    $paths = Get-VersionFilePaths -RepositoryPath $repository
    $before = @($paths.Manifest, $paths.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ','
    $version = Get-ReleaseVersion -RepositoryPath $repository
    $gitBefore = (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('status', '--porcelain', '--', $paths.Manifest, $paths.Source)).Output

    $result = Invoke-EntryScript -Command 'set-version' -Environment @{ VERSION = $version }
    Assert-Equal -Expected 0 -Actual $result.ExitCode -Name "set-version with the committed version exits zero ($($result.Output))"
    $after = @($paths.Manifest, $paths.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ','
    Assert-Equal -Expected $before -Actual $after -Name 'set-version with the committed version leaves both files unchanged'
    $gitAfter = (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('status', '--porcelain', '--', $paths.Manifest, $paths.Source)).Output
    Assert-Equal -Expected $gitBefore -Actual $gitAfter -Name 'git status for the version files is unchanged'

    $bad = Invoke-EntryScript -Command 'set-version' -Environment @{ VERSION = 'v1.2.3' }
    Assert-EntryScriptFails $bad 'Malformed version' 'set-version with a malformed version'
    Assert-Equal -Expected $before -Actual (@($paths.Manifest, $paths.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ',') -Name 'a rejected version leaves both files unchanged'

    $root = New-VersionRepository
    $elsewhere = Invoke-EntryScript -Command 'set-version' -Environment @{ VERSION = '7.8.9'; REPO_PATH = $root } -WorkingDirectory (New-TempDirectory)
    Assert-Equal -Expected 0 -Actual $elsewhere.ExitCode -Name "set-version honours REPO_PATH from another directory ($($elsewhere.Output))"
    Assert-Equal -Expected '7.8.9' -Actual (Get-ReleaseVersion -RepositoryPath $root) -Name 'REPO_PATH decides which tree is changed'
    Assert-Equal -Expected $before -Actual (@($paths.Manifest, $paths.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ',') -Name 'the working tree is untouched when REPO_PATH points elsewhere'
}

function New-VerifyTree {
    $root = New-TempDirectory
    $directory = Join-Path $root 'Codescene.VSExtension.VS2022'
    New-Item -ItemType Directory -Path $directory | Out-Null
    return [pscustomobject]@{ Root = $root; Directory = $directory }
}

function Invoke-VerifyVsix([string]$Root, [string]$Version) {
    $output = Join-Path (New-TempDirectory) 'output.txt'
    $result = Invoke-EntryScript -Command 'verify-vsix' -Environment @{ VERSION = $Version; REPO_PATH = $Root; GITHUB_OUTPUT = $output }
    return [pscustomobject]@{ Result = $result; Outputs = (Read-ActionOutput $output) }
}

function Test-VerifyVsixIntegration {
    $none = New-VerifyTree
    $missing = Invoke-VerifyVsix $none.Root '0.9.1'
    Assert-EntryScriptFails $missing.Result 'No \.vsix file found' 'verify-vsix with no vsix'
    Assert-Equal -Expected 0 -Actual $missing.Outputs.Count -Name 'verify-vsix with no vsix writes no output'

    $single = New-VerifyTree
    $vsix = New-VsixFile $single.Directory 'one.vsix' '0.9.1'
    $match = Invoke-VerifyVsix $single.Root '0.9.1'
    Assert-Equal -Expected 0 -Actual $match.Result.ExitCode -Name "verify-vsix accepts a matching version ($($match.Result.Output))"
    Assert-Equal -Expected $vsix -Actual $match.Outputs['path'] -Name 'verify-vsix writes the vsix path'
    $mismatch = Invoke-VerifyVsix $single.Root '0.9.2'
    Assert-EntryScriptFails $mismatch.Result 'VSIX version mismatch.*0\.9\.1.*0\.9\.2' 'verify-vsix with a differing version'
    Assert-Equal -Expected 0 -Actual $mismatch.Outputs.Count -Name 'verify-vsix with a differing version writes no output'

    $nested = New-VerifyTree
    New-Item -ItemType Directory -Path (Join-Path $nested.Directory 'Codescene.VSExtension.VS2022\bin\Release') -Force | Out-Null
    $nestedVsix = New-VsixFile (Join-Path $nested.Directory 'Codescene.VSExtension.VS2022\bin\Release') 'Codescene.VSExtension.VS2022.vsix' '0.9.1'
    $found = Invoke-VerifyVsix $nested.Root '0.9.1'
    Assert-Equal -Expected $nestedVsix -Actual $found.Outputs['path'] -Name 'verify-vsix finds a vsix in the nested build output'

    $two = New-VerifyTree
    New-VsixFile $two.Directory 'a.vsix' '0.9.1' | Out-Null
    New-VsixFile $two.Directory 'b.vsix' '0.9.1' | Out-Null
    $many = Invoke-VerifyVsix $two.Root '0.9.1'
    Assert-EntryScriptFails $many.Result 'exactly one \.vsix.*found 2' 'verify-vsix with two vsix files'

    $repository = $script:RepositoryRoot
    $built = Join-Path $repository 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\bin\Release\Codescene.VSExtension.VS2022.vsix'
    if (Test-Path -LiteralPath $built) {
        $real = New-VerifyTree
        Copy-Item -LiteralPath $built -Destination $real.Directory
        $embedded = Get-VsixManifestVersion -Path $built
        $realMatch = Invoke-VerifyVsix $real.Root $embedded
        Assert-Equal -Expected 0 -Actual $realMatch.Result.ExitCode -Name "verify-vsix accepts the local Release build ($($realMatch.Result.Output))"
        Assert-Equal -Expected (Join-Path $real.Directory (Split-Path -Leaf $built)) -Actual $realMatch.Outputs['path'] -Name 'verify-vsix writes the path of the local Release build'
        $other = if ($embedded -ceq '0.9.2') { '0.9.1' } else { '0.9.2' }
        Assert-EntryScriptFails (Invoke-VerifyVsix $real.Root $other).Result 'VSIX version mismatch' 'verify-vsix with the local Release build and another version'
    }
    else {
        Write-Host 'skipping local Release vsix check: no local Release build'
    }
}

function Get-TreeSnapshot([string]$Root) {
    $snapshot = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
        $snapshot[$file.FullName.Substring($Root.Length)] = "$($file.Length):$($file.LastWriteTimeUtc.Ticks)"
    }
    return $snapshot
}

function Test-HelperIndependentOfTaggedTree {
    if (-not (Get-TagCommit -Tag 'v0.9.1')) {
        Write-Host 'skipping archive check: tag v0.9.1 is not available locally'
        return
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $work = New-TempDirectory
    $zip = Join-Path $work 'tree.zip'
    $tree = Join-Path $work 'tree'
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('archive', '--format=zip', '-o', $zip, 'v0.9.1') | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tree)
    Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $tree '.github\release-flow.ps1'))) -Name 'the archived tree has no release-flow.ps1'
    Assert-Equal -Expected '0.9.1' -Actual (Get-ReleaseVersion -RepositoryPath $tree) -Name 'the archived tree carries version 0.9.1'

    $repository = $script:RepositoryRoot
    $paths = Get-VersionFilePaths -RepositoryPath $tree
    $committed = Get-VersionFilePaths -RepositoryPath $repository
    $committedBefore = @($committed.Manifest, $committed.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ','
    $before = Get-TreeSnapshot $tree
    $result = Invoke-EntryScript -Command 'set-version' -Environment @{ VERSION = '0.9.2'; REPO_PATH = $tree } -WorkingDirectory $repository
    Assert-Equal -Expected 0 -Actual $result.ExitCode -Name "set-version works on a tree without the helper ($($result.Output))"
    Assert-Equal -Expected '0.9.2' -Actual (Get-ReleaseVersion -RepositoryPath $tree) -Name 'the archived manifest is updated'
    Assert-True -Condition ((Read-AllText $paths.Source).Contains('Version = "0.9.2";')) -Name 'the archived source is updated'

    $after = Get-TreeSnapshot $tree
    Assert-Equal -Expected $before.Count -Actual $after.Count -Name 'no file is added to or removed from the archived tree'
    $changed = @($after.Keys | Where-Object { $before[$_] -cne $after[$_] } | Sort-Object)
    $expected = @($paths.Manifest, $paths.Source | ForEach-Object { $_.Substring($tree.Length) } | Sort-Object)
    Assert-Equal -Expected ($expected -join '|') -Actual ($changed -join '|') -Name 'only the two version files change in the archived tree'
    Assert-Equal -Expected $committedBefore -Actual (@($committed.Manifest, $committed.Source | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ',') -Name 'the working tree version files are untouched'
}
