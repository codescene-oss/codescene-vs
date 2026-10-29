function Test-WriteOverviewCommand {
    $root = New-VersionRepository
    $readme = "# CodeScene `u{00E9}`n<img src=`"screenshots/a.png`" />`n![x](screenshots/b.png)`n"
    Write-AllText (Join-Path $root 'README.md') $readme
    $path = Write-MarketplaceOverview -RepositoryPath $root
    $expectedPath = Join-Path $root 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\overview.md'
    Assert-Equal -Expected $expectedPath -Actual $path -Name 'the overview is written next to the version files'
    $bytes = [IO.File]::ReadAllBytes($path)
    Assert-True -Condition (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) -Name 'the overview has no byte order mark'
    Assert-Equal -Expected (ConvertTo-MarketplaceOverview -Readme $readme) -Actual (Read-AllText $path) -Name 'the overview is the converted readme'
    Assert-True -Condition ((Read-AllText $path).Contains("`u{00E9}")) -Name 'the overview keeps non-ASCII text as UTF-8'
    Assert-True -Condition ((Read-AllText $path).Contains('src="https://raw.githubusercontent.com/codescene-oss/codescene-vs/main/screenshots/a.png"')) -Name 'the overview rewrites html screenshot links'

    $viaScript = New-VersionRepository
    Write-AllText (Join-Path $viaScript 'README.md') $readme
    $run = Invoke-EntryScript -Command 'write-overview' -Environment @{ REPO_PATH = $viaScript } -WorkingDirectory (New-TempDirectory)
    Assert-Equal -Expected 0 -Actual $run.ExitCode -Name "write-overview exits zero ($($run.Output))"
    Assert-Equal -Expected (Read-AllText $path) -Actual (Read-AllText (Join-Path $viaScript 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\overview.md')) -Name 'write-overview writes the converted readme into REPO_PATH'

    $noReadme = New-VersionRepository
    $failed = Invoke-EntryScript -Command 'write-overview' -Environment @{ REPO_PATH = $noReadme }
    Assert-True -Condition ($failed.ExitCode -ne 0) -Name 'write-overview without a README fails'
}

function Test-WriteOverviewOnArchive {
    if (-not (Get-TagCommit -Tag 'v0.9.1')) {
        Write-Host 'skipping overview archive check: tag v0.9.1 is not available locally'
        return
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $work = New-TempDirectory
    $zip = Join-Path $work 'tree.zip'
    $tree = Join-Path $work 'tree'
    Invoke-NativeChecked -FilePath 'git' -ArgumentList @('archive', '--format=zip', '-o', $zip, 'v0.9.1') | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tree)
    Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $tree '.github\release-flow.ps1'))) -Name 'the archived tree has no release-flow.ps1 for write-overview'

    $repository = $script:RepositoryRoot
    $realOverview = Join-Path $repository 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\overview.md'
    $realBefore = Test-Path -LiteralPath $realOverview
    $overview = Join-Path $tree 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022\overview.md'
    Assert-True -Condition (-not (Test-Path -LiteralPath $overview)) -Name 'the archived tree starts without an overview'

    $run = Invoke-EntryScript -Command 'write-overview' -Environment @{ REPO_PATH = $tree } -WorkingDirectory $repository
    Assert-Equal -Expected 0 -Actual $run.ExitCode -Name "write-overview works on a tree without the helper ($($run.Output))"
    Assert-True -Condition (Test-Path -LiteralPath $overview) -Name 'write-overview writes overview.md into the archived tree'
    Assert-Equal -Expected (ConvertTo-MarketplaceOverview -Readme (Read-AllText (Join-Path $tree 'README.md'))) -Actual (Read-AllText $overview) -Name 'the archived overview is the converted readme'
    Assert-Equal -Expected $realBefore -Actual (Test-Path -LiteralPath $realOverview) -Name 'write-overview leaves the working tree overview alone'
}

function New-FakePublisherScript {
    $directory = New-TempDirectory
    $path = Join-Path $directory 'VsixPublisher.ps1'
    $script = @'
[IO.File]::AppendAllText($env:FAKE_PUBLISH_LOG, ($args -join '|') + "`n")
if ($env:FAKE_PUBLISH_OUTPUT) { $env:FAKE_PUBLISH_OUTPUT }
exit ([int]$env:FAKE_PUBLISH_EXIT)
'@
    Write-AllText $path $script
    return $path
}

function Invoke-PublishCommand([hashtable]$Environment, $Tree, [string]$PublisherPath) {
    $log = Join-Path (New-TempDirectory) 'publish.log'
    $variables = @{
        VSIX_PUBLISHER   = $PublisherPath
        VSIX_DIR         = $Tree.Root
        PUBLISH_MANIFEST = $Tree.Manifest
        FAKE_PUBLISH_LOG = $log
        FAKE_PUBLISH_EXIT = '0'
    }
    foreach ($name in $Environment.Keys) { $variables[$name] = $Environment[$name] }
    $result = Invoke-EntryScript -Command 'publish' -Environment $variables
    $calls = @()
    if (Test-Path -LiteralPath $log) { $calls = @((Read-AllText $log) -split "`n" | Where-Object { $_ }) }
    return [pscustomobject]@{ Result = $result; Calls = $calls }
}

function Test-PublishCommand {
    $tree = New-PublishTree
    $publisher = New-FakePublisherScript
    $base = @{ VERSION = '0.9.2' }

    foreach ($name in @('VERSION', 'VSIX_PUBLISHER', 'VSIX_DIR', 'PUBLISH_MANIFEST')) {
        $variables = @{ VERSION = '0.9.2'; VSIX_PUBLISHER = $publisher; VSIX_DIR = $tree.Root; PUBLISH_MANIFEST = $tree.Manifest }
        $variables.Remove($name)
        Assert-EntryScriptFails (Invoke-EntryScript -Command 'publish' -Environment $variables) "$name is required" "publish without $name"
    }

    $missing = Invoke-PublishCommand @{ VERSION = '0.9.2'; PUBLISH_MANIFEST = (Join-Path $tree.Root 'missing.json') } $tree $publisher
    Assert-EntryScriptFails $missing.Result 'publishManifest\.json not found' 'publish with a missing manifest'
    Assert-Equal -Expected 0 -Actual $missing.Calls.Count -Name 'publish with a missing manifest never calls the publisher'

    $reachable = $true
    try { Test-MarketplaceVersionListed -Version '0.9.1' | Out-Null } catch { $reachable = $false }
    if (-not $reachable) {
        Write-Host 'skipping publish integration: the Marketplace gallery is not reachable'
        return
    }

    $listed = Invoke-PublishCommand @{ VERSION = '0.9.1' } $tree $publisher
    Assert-Equal -Expected 0 -Actual $listed.Result.ExitCode -Name "publish of an already listed version exits zero ($($listed.Result.Output))"
    Assert-True -Condition ($listed.Result.Output -clike '*already listed*') -Name 'publish of a listed version logs the skip'
    Assert-Equal -Expected 0 -Actual $listed.Calls.Count -Name 'publish of a listed version never calls the publisher'

    $published = Invoke-PublishCommand @{ VERSION = '99.0.0'; FAKE_PUBLISH_OUTPUT = 'uploaded fine' } $tree $publisher
    Assert-Equal -Expected 0 -Actual $published.Result.ExitCode -Name "publish of an unlisted version exits zero ($($published.Result.Output))"
    Assert-Equal -Expected "publish|-payload|$($tree.Vsix)|-publishManifest|$($tree.Manifest)" -Actual ($published.Calls -join ';') -Name 'publish calls the publisher with the payload and manifest'
    Assert-True -Condition ($published.Result.Output -clike '*uploaded fine*') -Name 'publish echoes the publisher output'

    $relative = Invoke-PublishCommand @{ VERSION = '99.0.0'; REPO_PATH = (Split-Path -Parent $tree.Root); VSIX_DIR = (Split-Path -Leaf $tree.Root) } $tree $publisher
    Assert-Equal -Expected 0 -Actual $relative.Result.ExitCode -Name "publish with a VSIX_DIR relative to REPO_PATH exits zero ($($relative.Result.Output))"
    Assert-Equal -Expected "publish|-payload|$($tree.Vsix)|-publishManifest|$($tree.Manifest)" -Actual ($relative.Calls -join ';') -Name 'publish resolves a relative VSIX_DIR against REPO_PATH as the workflow passes it'

    $exists = Invoke-PublishCommand @{ VERSION = '99.0.0'; FAKE_PUBLISH_EXIT = '1'; FAKE_PUBLISH_OUTPUT = 'version already exists' } $tree $publisher
    Assert-Equal -Expected 0 -Actual $exists.Result.ExitCode -Name "publish treats an existing version as a skip ($($exists.Result.Output))"

    $failed = Invoke-PublishCommand @{ VERSION = '99.0.0'; FAKE_PUBLISH_EXIT = '1'; FAKE_PUBLISH_OUTPUT = 'boom' } $tree $publisher
    Assert-EntryScriptFails $failed.Result 'failed with exit code 1.*boom' 'publish with a failing publisher'
}
