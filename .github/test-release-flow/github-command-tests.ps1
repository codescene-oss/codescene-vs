function Test-AssetDownloadArguments {
    Assert-Equal -Expected 'release download v0.9.2 --pattern Codescene.VSExtension.VS2022.vsix --dir vsix --clobber' -Actual ((Get-AssetDownloadArguments -Tag 'v0.9.2' -Directory 'vsix') -join ' ') -Name 'the asset download arguments'
    Assert-Equal -Expected 8 -Actual @(Get-AssetDownloadArguments -Tag 'v0.9.2' -Directory 'C:\a dir\vsix').Count -Name 'a directory with spaces stays one argument'
}

function Test-GenerateNotesArguments {
    Assert-Equal -Expected 'api -X POST repos/o/r/releases/generate-notes -f tag_name=v0.9.2 -f previous_tag_name=v0.9.1' -Actual ((Get-GenerateNotesArguments -Repository 'o/r' -Tag 'v0.9.2' -PreviousTag 'v0.9.1') -join ' ') -Name 'generate-notes arguments with a previous tag'
    Assert-Equal -Expected 'api -X POST repos/o/r/releases/generate-notes -f tag_name=v0.9.2' -Actual ((Get-GenerateNotesArguments -Repository 'o/r' -Tag 'v0.9.2' -PreviousTag '') -join ' ') -Name 'generate-notes arguments omit an empty previous tag'
    Assert-Equal -Expected 'api -X POST repos/o/r/releases/generate-notes -f tag_name=v0.9.2' -Actual ((Get-GenerateNotesArguments -Repository 'o/r' -Tag 'v0.9.2' -PreviousTag $null) -join ' ') -Name 'generate-notes arguments omit a null previous tag'
}

function New-FakeInvoker([int]$ExitCode, [string]$Output) {
    $calls = New-Object System.Collections.ArrayList
    $script:InvokerCalls = $calls
    return {
        param($FilePath, $ArgumentList)
        $calls.Add("$FilePath $(@($ArgumentList) -join ' ')") | Out-Null
        [pscustomobject]@{ ExitCode = $ExitCode; Output = $Output }
    }.GetNewClosure()
}

function Test-LatestReleaseTag {
    $found = Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 0 '{"tag_name":"v0.9.1","draft":false}')
    Assert-Equal -Expected 'v0.9.1' -Actual $found -Name 'the latest release tag is returned'
    Assert-Equal -Expected 'gh api repos/o/r/releases/latest' -Actual ($script:InvokerCalls -join '|') -Name 'the latest release is read from the latest endpoint'

    $absent = Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 1 "{`"message`":`"Not Found`"}`ngh: Not Found (HTTP 404)")
    Assert-True -Condition ($null -eq $absent) -Name 'a 404 means no latest release'

    Assert-Throws -Action { Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 1 'gh: Server Error (HTTP 500)') } -MessagePattern 'exit code 1.*HTTP 500' -Name 'another failure throws'
    Assert-Throws -Action { Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 4 'gh: To use GitHub CLI, run: gh auth login') } -MessagePattern 'exit code 4.*gh auth login' -Name 'an authentication failure throws'
    Assert-Throws -Action { Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 0 '{"name":"x"}') } -MessagePattern 'no tag name' -Name 'a response without a tag name throws'

    $mentions = Get-LatestReleaseTag -Repository 'o/r' -Invoker (New-FakeInvoker 0 '{"tag_name":"v0.9.1","body":"Fixes HTTP 404 handling"}')
    Assert-Equal -Expected 'v0.9.1' -Actual $mentions -Name 'a successful response that mentions HTTP 404 still returns the tag'
}

function Test-TestRunQuery {
    $sha = '0123456789abcdef0123456789abcdef01234567'
    $absent = @(Get-TestRun -Repository 'o/r' -Sha $sha -Invoker (New-FakeInvoker 1 "{`"message`":`"Not Found`"}`ngh: Not Found (HTTP 404)"))
    Assert-Equal -Expected 0 -Actual $absent.Count -Name 'a 404 gives an empty run list'
    Assert-Equal -Expected 'absent' -Actual (Get-TestGateVerdict -Runs $absent -Sha $sha).Reason -Name 'a 404 gives an absent verdict'
    Assert-Equal -Expected "gh api repos/o/r/actions/workflows/test.yml/runs?head_sha=$sha&per_page=100" -Actual ($script:InvokerCalls -join '|') -Name 'the runs are queried by head sha'

    $payload = '{"total_count":2,"workflow_runs":[{"status":"completed","conclusion":"failure","event":"pull_request","html_url":"https://github.com/o/r/actions/runs/7"},{"status":"completed","conclusion":"success","event":"push","html_url":"https://github.com/o/r/actions/runs/42"}]}'
    $runs = @(Get-TestRun -Repository 'o/r' -Sha $sha -Invoker (New-FakeInvoker 0 $payload))
    Assert-Equal -Expected 2 -Actual $runs.Count -Name 'the workflow runs are returned'
    $verdict = Get-TestGateVerdict -Runs $runs -Sha $sha
    Assert-Equal -Expected 'pass' -Actual $verdict.Reason -Name 'a success run gives a pass verdict'
    Assert-True -Condition ($verdict.Message -clike '*actions/runs/42*') -Name 'the pass verdict links the successful run'

    $single = @(Get-TestRun -Repository 'o/r' -Sha $sha -Invoker (New-FakeInvoker 0 '{"workflow_runs":[{"status":"queued","conclusion":null,"html_url":"u"}]}'))
    Assert-Equal -Expected 1 -Actual $single.Count -Name 'a single run is returned as one entry'
    Assert-Equal -Expected 'in-progress' -Actual (Get-TestGateVerdict -Runs $single -Sha $sha).Reason -Name 'a queued run gives an in-progress verdict'

    $empty = @(Get-TestRun -Repository 'o/r' -Sha $sha -Invoker (New-FakeInvoker 0 '{"total_count":0,"workflow_runs":[]}'))
    Assert-Equal -Expected 0 -Actual $empty.Count -Name 'an empty run list stays empty'

    Assert-Throws -Action { Get-TestRun -Repository 'o/r' -Sha $sha -Invoker (New-FakeInvoker 1 'gh: Bad credentials (HTTP 401)') } -MessagePattern 'exit code 1.*HTTP 401' -Name 'another failure throws instead of reading as absent'
}

function Test-ReleaseNotesFile {
    $saved = $env:RUNNER_TEMP
    $directory = New-TempDirectory
    $body = "## Changes`n- caf$([char]0xE9) $([char]0x2192) ok"
    try {
        $env:RUNNER_TEMP = $directory
        $path = Write-ReleaseNotesFile -Tag 'v0.9.2' -Body $body
        Assert-Equal -Expected (Join-Path $directory 'release-notes-v0.9.2.md') -Actual $path -Name 'the notes file is written to RUNNER_TEMP'
        $bytes = [IO.File]::ReadAllBytes($path)
        Assert-True -Condition (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) -Name 'the notes file has no BOM'
        Assert-Equal -Expected $body -Actual (New-Object System.Text.UTF8Encoding($false)).GetString($bytes) -Name 'the notes file keeps the body as UTF-8'

        $empty = Write-ReleaseNotesFile -Tag 'v0.9.3' -Body ''
        Assert-Equal -Expected 0 -Actual ([IO.File]::ReadAllBytes($empty)).Length -Name 'an empty body writes an empty file'

        $env:RUNNER_TEMP = $null
        $fallback = Write-ReleaseNotesFile -Tag 'v0.9.4' -Body 'x'
        try {
            Assert-Equal -Expected ([IO.Path]::GetTempPath()) -Actual ((Split-Path -Parent $fallback) + [IO.Path]::DirectorySeparatorChar) -Name 'the notes file falls back to the temp directory'
        }
        finally {
            Remove-Item -LiteralPath $fallback -Force -ErrorAction SilentlyContinue
        }
    }
    finally {
        $env:RUNNER_TEMP = $saved
    }
}

function Test-GitHubReadIntegration {
    $token = Get-GitHubTokenForTests
    if (-not $token) {
        Write-Host 'skipping GitHub read integration: no GitHub token available'
        return
    }
    $repository = 'codescene-oss/codescene-vs'
    $latest = Get-LatestReleaseTag -Repository $repository
    if ($latest -cne 'v0.9.1') {
        Write-Host "skipping GitHub read integration: the latest release is '$latest', not v0.9.1"
        return
    }
    Assert-Equal -Expected 'v0.9.1' -Actual $latest -Name 'the real latest release is v0.9.1'
    Assert-True -Condition (Test-ReleaseExists -Tag 'v0.9.1') -Name 'the real release v0.9.1 exists'
    Assert-True -Condition (-not (Test-ReleaseExists -Tag 'v9.9.9')) -Name 'a missing release does not exist'
    Assert-Equal -Expected 'Codescene.VSExtension.VS2022.vsix' -Actual (@(Get-ReleaseAssetName -Tag 'v0.9.1') -join ',') -Name 'the real release carries the vsix asset'

    $notes = Get-GeneratedReleaseNotes -Repository $repository -Tag 'v0.9.1' -PreviousTag 'v0.9.0'
    Assert-True -Condition ($notes.Length -gt 0) -Name 'generate-notes returns a body'
    Assert-True -Condition ($notes -clike '*v0.9.0...v0.9.1*') -Name 'generate-notes compares against the previous tag'
    $composed = Get-ReleaseNotesBody -Kind 'test' -Generated $notes -Sha '01f198db079248e461264ef79f94e82d8bd6faa8' -ServerUrl 'https://github.com' -Repository $repository
    Assert-True -Condition ($composed.StartsWith('Test build, not published', [StringComparison]::Ordinal)) -Name 'the generated notes get the test banner'
    Assert-Equal -Expected $notes -Actual (Get-ReleaseNotesBody -Kind 'stable' -Generated $notes -Sha '' -ServerUrl '' -Repository $repository) -Name 'the generated notes stay unchanged for a stable release'

    $sha = '01f198db079248e461264ef79f94e82d8bd6faa8'
    $runs = @(Get-TestRun -Repository $repository -Sha $sha)
    $gate = Invoke-EntryScript -Command 'test-gate' -Environment @{ GH_TOKEN = $token; SHA = $sha }
    if ($runs.Count -eq 0) {
        Assert-EntryScriptFails $gate "No 'Run tests' run found for commit $sha" 'test-gate before test.yml has run for the sha'
    }
    else {
        $verdict = Get-TestGateVerdict -Runs $runs -Sha $sha
        Assert-Equal -Expected $verdict.Pass -Actual ($gate.ExitCode -eq 0) -Name 'test-gate agrees with the verdict of the real runs'
    }

    $directory = New-TempDirectory
    $download = Invoke-EntryScript -Command 'download-asset' -Environment @{ GH_TOKEN = $token; TAG = 'v0.9.1'; ASSET_DIR = $directory }
    Assert-Equal -Expected 0 -Actual $download.ExitCode -Name "download-asset of v0.9.1 exits zero ($($download.Output))"
    Assert-Equal -Expected 'Codescene.VSExtension.VS2022.vsix' -Actual (@(Get-ChildItem -LiteralPath $directory -Force | ForEach-Object { $_.Name }) -join ',') -Name 'download-asset leaves exactly the vsix in the directory'
    Assert-Equal -Expected '0.9.1' -Actual (Get-VsixManifestVersion -Path (Join-Path $directory 'Codescene.VSExtension.VS2022.vsix')) -Name 'the downloaded vsix embeds version 0.9.1'
    $again = Invoke-EntryScript -Command 'download-asset' -Environment @{ GH_TOKEN = $token; TAG = 'v0.9.1'; ASSET_DIR = $directory }
    Assert-Equal -Expected 0 -Actual $again.ExitCode -Name "download-asset overwrites an existing file ($($again.Output))"

    $missing = Invoke-EntryScript -Command 'download-asset' -Environment @{ GH_TOKEN = $token; TAG = 'v9.9.9'; ASSET_DIR = (New-TempDirectory) }
    Assert-True -Condition ($missing.ExitCode -ne 0) -Name 'download-asset of a missing release fails'
}
