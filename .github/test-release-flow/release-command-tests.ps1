function New-FakeGh {
    $directory = New-TempDirectory
    $script = @'
$line = $args -join ' '
[IO.File]::AppendAllText($env:FAKE_GH_LOG, "$line`n")
switch -Wildcard ($line) {
    'api -X POST *generate-notes*' { '{"body":"## Changes\n- a change"}'; exit 0 }
    'release view * --json tagName' {
        if ($env:FAKE_RELEASE_EXISTS -eq 'true') { '{"tagName":"x"}'; exit 0 }
        'release not found'; exit 1
    }
    'release view * --json assets' {
        $names = @($env:FAKE_ASSETS -split ',' | Where-Object { $_ })
        $items = @($names | ForEach-Object { '{"name":"' + $_ + '"}' })
        '{"assets":[' + ($items -join ',') + ']}'; exit 0
    }
    'auth status' {
        if ($env:FAKE_AUTH -eq 'fail') { 'You are not logged into any GitHub hosts.'; exit 1 }
        exit 0
    }
    'api --paginate --slurp repos/*/releases*' { '[[' + $env:FAKE_RELEASES + ']]'; exit 0 }
    'api repos/*/releases/latest' {
        if ($env:FAKE_LATEST) { '{"tag_name":"' + $env:FAKE_LATEST + '"}'; exit 0 }
        '{"message":"Not Found"}'; 'gh: Not Found (HTTP 404)'; exit 1
    }
    'api repos/*/actions/workflows/test.yml/runs*' {
        if ($env:FAKE_RUNS -eq 'success') { '{"workflow_runs":[{"status":"completed","conclusion":"success","html_url":"https://github.com/o/r/actions/runs/42"}]}'; exit 0 }
        if ($env:FAKE_RUNS -eq 'failure') { '{"workflow_runs":[{"status":"completed","conclusion":"failure","html_url":"https://github.com/o/r/actions/runs/7"}]}'; exit 0 }
        '{"message":"Not Found"}'; 'gh: Not Found (HTTP 404)'; exit 1
    }
    'release create *' { exit 0 }
    'release edit *' { exit 0 }
    'release upload *' { exit 0 }
    'release download *' { exit 0 }
    default { "unexpected gh call: $line"; exit 9 }
}
'@
    [IO.File]::WriteAllText((Join-Path $directory 'gh.ps1'), $script, (New-Object System.Text.UTF8Encoding($false)))
    return $directory
}

function Invoke-FakeGhCommand([string]$Command, [hashtable]$Environment) {
    $fake = New-FakeGh
    $log = Join-Path (New-TempDirectory) 'gh.log'
    $notes = New-TempDirectory
    $variables = @{
        GH_TOKEN          = 'unused'
        GITHUB_REPOSITORY = 'o/r'
        RUNNER_TEMP       = $notes
        FAKE_GH_LOG       = $log
        PATH              = "$fake;$env:PATH"
    }
    foreach ($name in $Environment.Keys) { $variables[$name] = $Environment[$name] }
    $result = Invoke-EntryScript -Command $Command -Environment $variables
    $calls = @()
    if (Test-Path -LiteralPath $log) { $calls = @((Read-AllText $log) -split "`n" | Where-Object { $_ }) }
    return [pscustomobject]@{ Result = $result; Calls = $calls; Notes = $notes }
}

function Get-FakeGhCall($Run, [string]$Prefix) {
    return , @($Run.Calls | Where-Object { $_.StartsWith($Prefix, [StringComparison]::Ordinal) })
}

function Assert-FakeGhPasses($Run, [string]$Name) {
    Assert-Equal -Expected 0 -Actual $Run.Result.ExitCode -Name "$Name exits zero ($($Run.Result.Output))"
}

function Test-CreateReleaseCommand {
    $notesApi = 'api -X POST repos/o/r/releases/generate-notes'

    $fresh = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2'; KIND = 'stable'; NOTES_START = 'v0.9.1' }
    Assert-FakeGhPasses $fresh 'create-release of a new stable release'
    Assert-Equal -Expected "$notesApi -f tag_name=v0.9.2 -f previous_tag_name=v0.9.1" -Actual (Get-FakeGhCall $fresh $notesApi)[0] -Name 'create-release generates notes since the previous release'
    $notesFile = Join-Path $fresh.Notes 'release-notes-v0.9.2.md'
    Assert-Equal -Expected "release create v0.9.2 --title v0.9.2 --notes-file $notesFile --verify-tag --latest=false" -Actual (Get-FakeGhCall $fresh 'release create')[0] -Name 'create-release creates a non-latest release'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $fresh 'release edit').Count -Name 'create-release does not edit a new release'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $fresh 'api repos/o/r/releases/latest').Count -Name 'create-release does not read the latest release for a new release'
    Assert-Equal -Expected "## Changes`n- a change" -Actual (Read-AllText $notesFile) -Name 'create-release writes the generated notes unchanged for a stable release'

    $noStart = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2'; KIND = 'stable' }
    Assert-FakeGhPasses $noStart 'create-release without a previous release'
    Assert-Equal -Expected "$notesApi -f tag_name=v0.9.2" -Actual (Get-FakeGhCall $noStart $notesApi)[0] -Name 'create-release omits the previous tag when the start is empty'

    $latestSame = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.1'; KIND = 'stable'; NOTES_START = 'v0.9.0'; FAKE_RELEASE_EXISTS = 'true'; FAKE_LATEST = 'v0.9.1' }
    Assert-FakeGhPasses $latestSame 'create-release of an existing latest release'
    $sameNotes = Join-Path $latestSame.Notes 'release-notes-v0.9.1.md'
    Assert-Equal -Expected "release edit v0.9.1 --title v0.9.1 --notes-file $sameNotes --latest=true" -Actual (Get-FakeGhCall $latestSame 'release edit')[0] -Name 'create-release keeps the latest marker on the latest release'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $latestSame 'release create').Count -Name 'create-release does not create an existing release'

    $latestOther = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.0'; KIND = 'stable'; FAKE_RELEASE_EXISTS = 'true'; FAKE_LATEST = 'v0.9.1' }
    Assert-FakeGhPasses $latestOther 'create-release of an older existing release'
    Assert-True -Condition ((Get-FakeGhCall $latestOther 'release edit')[0].EndsWith('--latest=false', [StringComparison]::Ordinal)) -Name 'create-release does not take the latest marker from a newer release'

    $noLatest = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.0'; KIND = 'stable'; FAKE_RELEASE_EXISTS = 'true' }
    Assert-FakeGhPasses $noLatest 'create-release of an existing release when no release is latest'
    Assert-True -Condition ((Get-FakeGhCall $noLatest 'release edit')[0].EndsWith('--latest=false', [StringComparison]::Ordinal)) -Name 'create-release treats a missing latest release as not latest'

    $sha = '01f198db079248e461264ef79f94e82d8bd6faa8'
    $test = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2-test.01f198d'; KIND = 'test'; NOTES_START = 'v0.9.1'; SHA = $sha; GITHUB_SERVER_URL = 'https://github.com' }
    Assert-FakeGhPasses $test 'create-release of a new test release'
    $testNotes = Join-Path $test.Notes 'release-notes-v0.9.2-test.01f198d.md'
    Assert-Equal -Expected "release create v0.9.2-test.01f198d --title v0.9.2-test.01f198d --notes-file $testNotes --verify-tag --latest=false --prerelease" -Actual (Get-FakeGhCall $test 'release create')[0] -Name 'create-release creates a test release as a prerelease'
    Assert-Equal -Expected "Test build, not published to the Visual Studio Marketplace. Commit: [01f198d](https://github.com/o/r/commit/$sha)`n`n## Changes`n- a change" -Actual (Read-AllText $testNotes) -Name 'create-release prefixes the test banner'

    $existingTest = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2-test.01f198d'; KIND = 'test'; SHA = $sha; GITHUB_SERVER_URL = 'https://github.com'; FAKE_RELEASE_EXISTS = 'true'; FAKE_LATEST = 'v0.9.1' }
    Assert-FakeGhPasses $existingTest 'create-release of an existing test release'
    Assert-True -Condition ((Get-FakeGhCall $existingTest 'release edit')[0].EndsWith('--latest=false', [StringComparison]::Ordinal)) -Name 'create-release never marks a test release latest'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $existingTest 'api repos/o/r/releases/latest').Count -Name 'create-release does not read the latest release for a test release'

    $noSha = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2-test.01f198d'; KIND = 'test'; GITHUB_SERVER_URL = 'https://github.com' }
    Assert-EntryScriptFails $noSha.Result 'SHA is required' 'create-release of a test release without a sha'
    Assert-Equal -Expected 0 -Actual $noSha.Calls.Count -Name 'create-release without a sha makes no gh call'

    $mismatch = Invoke-FakeGhCommand 'create-release' @{ TAG = 'v0.9.2'; KIND = 'test' }
    Assert-EntryScriptFails $mismatch.Result 'does not match tag v0\.9\.2' 'create-release with a kind that differs from the tag'
    Assert-Equal -Expected 0 -Actual $mismatch.Calls.Count -Name 'create-release with a wrong kind makes no gh call'

    $noTag = Invoke-FakeGhCommand 'create-release' @{ KIND = 'stable' }
    Assert-EntryScriptFails $noTag.Result 'TAG is required' 'create-release without a tag'

    $badTag = Invoke-FakeGhCommand 'create-release' @{ TAG = '..\evil'; KIND = 'stable' }
    Assert-EntryScriptFails $badTag.Result 'Malformed tag' 'create-release with a malformed tag'
    Assert-Equal -Expected 0 -Actual $badTag.Calls.Count -Name 'create-release with a malformed tag makes no gh call'
}

function Test-UploadCommand {
    $assetName = 'Codescene.VSExtension.VS2022.vsix'
    $directory = New-TempDirectory
    $vsix = Join-Path $directory $assetName
    [IO.File]::WriteAllText($vsix, 'x')
    [IO.File]::WriteAllText((Join-Path $directory 'notes.txt'), 'y')

    $upload = Invoke-FakeGhCommand 'upload' @{ TAG = 'v0.9.2'; ASSET_DIR = $directory }
    Assert-FakeGhPasses $upload 'upload of a missing asset'
    Assert-Equal -Expected "release upload v0.9.2 $vsix" -Actual (Get-FakeGhCall $upload 'release upload')[0] -Name 'upload attaches the vsix with its file name'
    Assert-Equal -Expected 1 -Actual (Get-FakeGhCall $upload 'release upload').Count -Name 'upload attaches only vsix files'

    $skip = Invoke-FakeGhCommand 'upload' @{ TAG = 'v0.9.2'; ASSET_DIR = $directory; FAKE_ASSETS = $assetName }
    Assert-FakeGhPasses $skip 'upload of an attached asset'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $skip 'release upload').Count -Name 'upload skips an asset that is attached already'
    Assert-True -Condition ($skip.Result.Output -clike "*Skipping $assetName*") -Name 'upload logs the skipped asset'

    $other = Invoke-FakeGhCommand 'upload' @{ TAG = 'v0.9.2'; ASSET_DIR = $directory; FAKE_ASSETS = 'other.zip' }
    Assert-Equal -Expected 1 -Actual (Get-FakeGhCall $other 'release upload').Count -Name 'upload attaches the vsix next to unrelated assets'

    $empty = Invoke-FakeGhCommand 'upload' @{ TAG = 'v0.9.2'; ASSET_DIR = (New-TempDirectory) }
    Assert-EntryScriptFails $empty.Result 'No \.vsix file found' 'upload from a directory without a vsix'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $empty 'release upload').Count -Name 'upload from an empty directory attaches nothing'

    $noDirectory = Invoke-FakeGhCommand 'upload' @{ TAG = 'v0.9.2' }
    Assert-EntryScriptFails $noDirectory.Result 'ASSET_DIR is required' 'upload without an asset directory'
}

function Test-DownloadAssetCommand {
    $directory = New-TempDirectory
    $missing = Invoke-FakeGhCommand 'download-asset' @{ TAG = 'v0.9.2'; ASSET_DIR = $directory }
    Assert-Equal -Expected "release download v0.9.2 --pattern Codescene.VSExtension.VS2022.vsix --dir $directory --clobber" -Actual (Get-FakeGhCall $missing 'release download')[0] -Name 'download-asset runs the download arguments'
    Assert-EntryScriptFails $missing.Result 'No \.vsix file found' 'download-asset when the asset is missing'

    $noDirectory = Invoke-FakeGhCommand 'download-asset' @{ TAG = 'v0.9.2' }
    Assert-EntryScriptFails $noDirectory.Result 'ASSET_DIR is required' 'download-asset without an asset directory'
}

function Test-TestGateCommand {
    $sha = '01f198db079248e461264ef79f94e82d8bd6faa8'
    $pass = Invoke-FakeGhCommand 'test-gate' @{ SHA = $sha; FAKE_RUNS = 'success' }
    Assert-FakeGhPasses $pass 'test-gate with a green run'
    Assert-True -Condition ($pass.Result.Output -clike '*actions/runs/42*') -Name 'test-gate prints the pass message'
    Assert-Equal -Expected "api repos/o/r/actions/workflows/test.yml/runs?head_sha=$sha&per_page=100" -Actual (Get-FakeGhCall $pass 'api')[0] -Name 'test-gate queries the runs of the sha'

    $failed = Invoke-FakeGhCommand 'test-gate' @{ SHA = $sha; FAKE_RUNS = 'failure' }
    Assert-EntryScriptFails $failed.Result "No successful 'Run tests' run for commit $sha" 'test-gate with a red run'

    $absent = Invoke-FakeGhCommand 'test-gate' @{ SHA = $sha }
    Assert-EntryScriptFails $absent.Result "No 'Run tests' run found for commit $sha" 'test-gate when the workflow is absent'

    $noSha = Invoke-FakeGhCommand 'test-gate' @{}
    Assert-EntryScriptFails $noSha.Result 'SHA is required' 'test-gate without a sha'
}

function Test-PromoteCommand {
    $first = Invoke-FakeGhCommand 'promote' @{ TAG = 'v0.9.2' }
    Assert-FakeGhPasses $first 'promote when no release is latest'
    Assert-Equal -Expected 'release edit v0.9.2 --latest' -Actual (Get-FakeGhCall $first 'release edit')[0] -Name 'promote marks the release latest when none is'

    $newer = Invoke-FakeGhCommand 'promote' @{ TAG = 'v0.10.0'; FAKE_LATEST = 'v0.9.9' }
    Assert-Equal -Expected 'release edit v0.10.0 --latest' -Actual (Get-FakeGhCall $newer 'release edit')[0] -Name 'promote marks a higher version latest'

    $same = Invoke-FakeGhCommand 'promote' @{ TAG = 'v0.9.2'; FAKE_LATEST = 'v0.9.2' }
    Assert-FakeGhPasses $same 'promote of the latest release'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $same 'release edit').Count -Name 'promote leaves the latest release unchanged'

    $superseded = Invoke-FakeGhCommand 'promote' @{ TAG = 'v0.9.1'; FAKE_LATEST = 'v0.10.0' }
    Assert-FakeGhPasses $superseded 'promote of a superseded release'
    Assert-Equal -Expected 0 -Actual (Get-FakeGhCall $superseded 'release edit').Count -Name 'promote does not demote a newer latest release'
    Assert-True -Condition ($superseded.Result.Output -clike '*superseded by v0.10.0*') -Name 'promote logs the superseding release'

    $testTag = Invoke-FakeGhCommand 'promote' @{ TAG = 'v0.9.2-test.01f198d'; FAKE_LATEST = 'v0.9.1' }
    Assert-EntryScriptFails $testTag.Result 'Only a stable release can be promoted' 'promote of a test tag'
    Assert-Equal -Expected 0 -Actual $testTag.Calls.Count -Name 'promote of a test tag makes no gh call'
}
