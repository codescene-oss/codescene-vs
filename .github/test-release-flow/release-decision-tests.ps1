function Test-TestReleaseNotes {
    $sha = '0123456789abcdef0123456789abcdef01234567'
    $generated = "## What's Changed`n* Fix thing by @someone in #1`n`n**Full Changelog**: https://example.test/compare/a...b"
    $notes = Format-TestReleaseNotes -Generated $generated -Sha $sha -ServerUrl 'https://github.com' -Repository 'codescene-oss/codescene-vs'
    $lines = $notes -split "`n"
    Assert-Equal -Expected 'Test build, not published to the Visual Studio Marketplace. Commit: [0123456](https://github.com/codescene-oss/codescene-vs/commit/0123456789abcdef0123456789abcdef01234567)' -Actual $lines[0] -Name 'test notes start with the banner and commit link'
    Assert-Equal -Expected '' -Actual $lines[1] -Name 'test notes have a blank line after the banner'
    Assert-Equal -Expected $generated -Actual ($lines[2..($lines.Count - 1)] -join "`n") -Name 'test notes keep the generated text verbatim'
    Assert-Equal -Expected 1 -Actual @($lines | Where-Object { $_ -clike 'Test build*' }).Count -Name 'test notes carry a single banner'

    $empty = Format-TestReleaseNotes -Generated '' -Sha $sha -ServerUrl 'https://github.com' -Repository 'o/r'
    Assert-True -Condition ($empty -cmatch '^Test build.*\(https://github\.com/o/r/commit/0123456789abcdef0123456789abcdef01234567\)\n\n$') -Name 'empty generated notes still yield the banner'

    Assert-Equal -Expected $generated -Actual (Get-ReleaseNotesBody -Kind 'stable' -Generated $generated -Sha $sha -ServerUrl 'https://github.com' -Repository 'o/r') -Name 'stable notes are unchanged'
    Assert-Equal -Expected $generated -Actual (Get-ReleaseNotesBody -Kind 'stable' -Generated $generated) -Name 'stable notes need no commit details'
    Assert-Equal -Expected $notes -Actual (Get-ReleaseNotesBody -Kind 'test' -Generated $generated -Sha $sha -ServerUrl 'https://github.com' -Repository 'codescene-oss/codescene-vs') -Name 'test notes body is the formatted notes'
    Assert-Throws -Action { Get-ReleaseNotesBody -Kind 'beta' -Generated $generated } -MessagePattern 'Unknown tag kind' -Name 'an unknown kind throws for the notes body'
}

function Get-ReleaseConfigSection([string[]]$Lines, [string]$Start, [string]$End) {
    $from = [array]::IndexOf($Lines, $Start)
    if ($from -lt 0) { return @() }
    $to = if ($End) { [array]::IndexOf($Lines, $End) } else { $Lines.Count }
    if ($to -lt $from) { return @() }
    return @($Lines[($from + 1)..($to - 1)])
}

function Test-ReleaseNotesConfig {
    $path = Join-Path $script:RepositoryRoot '.github\release.yml'
    Assert-True -Condition (Test-Path -LiteralPath $path) -Name 'the release notes configuration exists'
    $text = [System.IO.File]::ReadAllText($path)
    $lines = @($text -split '\r?\n' | Where-Object { $_ -ne '' })

    Assert-Equal -Expected 'changelog:' -Actual $lines[0] -Name 'the configuration is rooted at changelog'
    Assert-Equal -Expected 0 -Actual @($lines | Where-Object { $_ -match '#' }).Count -Name 'the configuration carries no comments'

    $titles = @($lines | Where-Object { $_ -cmatch '^    - title: ' } | ForEach-Object { $_ -creplace '^    - title: ', '' })
    Assert-Equal -Expected 'Breaking Changes|Features|Bug Fixes|Security|Documentation|Dependencies|Other Changes' -Actual ($titles -join '|') -Name 'the seven categories appear in order'

    $exclude = Get-ReleaseConfigSection -Lines $lines -Start '  exclude:' -End '  categories:'
    $excludedLabels = Get-ReleaseConfigSection -Lines $exclude -Start '    labels:' -End '    authors:'
    $excludedAuthors = Get-ReleaseConfigSection -Lines $exclude -Start '    authors:' -End $null
    Assert-Equal -Expected '      - ignore-for-release|      - skip-changelog' -Actual ($excludedLabels -join '|') -Name 'the excluded labels are listed'
    Assert-Equal -Expected '      - dependabot|      - github-actions' -Actual ($excludedAuthors -join '|') -Name 'the excluded authors are listed'

    $expectedLabels = [ordered]@{
        'Breaking Changes' = 'breaking-change|breaking'
        'Features'         = 'feature|enhancement'
        'Bug Fixes'        = 'bug|fix|defect'
        'Security'         = 'security'
        'Documentation'    = 'documentation|docs'
        'Dependencies'     = 'dependencies'
        'Other Changes'    = '"*"'
    }
    foreach ($title in $expectedLabels.Keys) {
        $start = "    - title: $title"
        $next = @($lines | Where-Object { $_ -cmatch '^    - title: ' -and $_ -cne $start } | Where-Object { [array]::IndexOf($lines, $_) -gt [array]::IndexOf($lines, $start) } | Select-Object -First 1)
        $end = if ($next.Count -gt 0) { $next[0] } else { $null }
        $section = Get-ReleaseConfigSection -Lines $lines -Start $start -End $end
        Assert-Equal -Expected '      labels:' -Actual $section[0] -Name "$title lists labels"
        $labels = @($section | Select-Object -Skip 1 | ForEach-Object { $_ -creplace '^        - ', '' })
        Assert-Equal -Expected $expectedLabels[$title] -Actual ($labels -join '|') -Name "$title has the expected labels"
    }

    $wildcards = @($lines | Where-Object { $_ -match '\*' })
    Assert-Equal -Expected 1 -Actual $wildcards.Count -Name 'the wildcard appears exactly once'
    Assert-Equal -Expected '        - "*"' -Actual $wildcards[0] -Name 'the wildcard is quoted'
    Assert-Equal -Expected ($lines.Count - 1) -Actual ([array]::IndexOf($lines, $wildcards[0])) -Name 'the wildcard is the last entry, under Other Changes'
}

function Test-ReleaseCommandArguments {
    $createStable = @(Get-ReleaseCommandArguments -Exists $false -Kind 'stable' -Tag 'v0.9.2' -NotesFile 'n.md' -IsLatest $false)
    Assert-Equal -Expected 'create v0.9.2 --title v0.9.2 --notes-file n.md --verify-tag --latest=false' -Actual ($createStable -join ' ') -Name 'create stable arguments'

    $createTest = @(Get-ReleaseCommandArguments -Exists $false -Kind 'test' -Tag 'v0.9.2-test.abc1234' -NotesFile 'n.md' -IsLatest $false)
    Assert-Equal -Expected 'create v0.9.2-test.abc1234 --title v0.9.2-test.abc1234 --notes-file n.md --verify-tag --latest=false --prerelease' -Actual ($createTest -join ' ') -Name 'create test arguments'

    $createIgnoresLatest = @(Get-ReleaseCommandArguments -Exists $false -Kind 'stable' -Tag 'v0.9.2' -NotesFile 'n.md' -IsLatest $true)
    Assert-Equal -Expected ($createStable -join ' ') -Actual ($createIgnoresLatest -join ' ') -Name 'a new release is never created as latest'

    $editLatest = @(Get-ReleaseCommandArguments -Exists $true -Kind 'stable' -Tag 'v0.9.2' -NotesFile 'n.md' -IsLatest $true)
    Assert-Equal -Expected 'edit v0.9.2 --title v0.9.2 --notes-file n.md --latest=true' -Actual ($editLatest -join ' ') -Name 'edit while latest keeps latest'

    $editOlder = @(Get-ReleaseCommandArguments -Exists $true -Kind 'stable' -Tag 'v0.9.0' -NotesFile 'n.md' -IsLatest $false)
    Assert-Equal -Expected 'edit v0.9.0 --title v0.9.0 --notes-file n.md --latest=false' -Actual ($editOlder -join ' ') -Name 'edit while not latest keeps it not latest'

    $editTest = @(Get-ReleaseCommandArguments -Exists $true -Kind 'test' -Tag 'v0.9.2-test.abc1234' -NotesFile 'n.md' -IsLatest $false)
    foreach ($arguments in @($editLatest, $editOlder, $editTest)) {
        Assert-True -Condition (($arguments -contains '--prerelease') -eq $false) -Name 'edit never passes --prerelease'
        Assert-True -Condition (($arguments -contains '--verify-tag') -eq $false) -Name 'edit never passes --verify-tag'
    }
    Assert-Throws -Action { Get-ReleaseCommandArguments -Exists $false -Kind 'beta' -Tag 'v0.9.2' -NotesFile 'n.md' -IsLatest $false } -MessagePattern 'Unknown tag kind' -Name 'an unknown kind throws for release arguments'
}

function Test-MissingAssets {
    $paths = @('C:\out\a.vsix', 'C:\out\publishManifest.json')
    Assert-Equal -Expected ($paths -join ',') -Actual (@(Select-MissingAsset -ExistingNames @() -Paths $paths) -join ',') -Name 'no attached assets returns all paths'
    Assert-Equal -Expected ($paths -join ',') -Actual (@(Select-MissingAsset -ExistingNames $null -Paths $paths) -join ',') -Name 'a null attached list returns all paths'
    Assert-Equal -Expected 'C:\out\publishManifest.json' -Actual (@(Select-MissingAsset -ExistingNames @('a.vsix') -Paths $paths) -join ',') -Name 'an attached name is skipped whatever its directory'
    Assert-Equal -Expected 0 -Actual @(Select-MissingAsset -ExistingNames @('a.vsix', 'publishManifest.json') -Paths $paths).Count -Name 'everything attached returns nothing'
    Assert-Equal -Expected 'C:\out\A.vsix' -Actual (@(Select-MissingAsset -ExistingNames @('a.vsix') -Paths @('C:\out\A.vsix')) -join ',') -Name 'asset names are compared case-sensitively'
    Assert-Equal -Expected 0 -Actual @(Select-MissingAsset -ExistingNames @('a.vsix') -Paths @()).Count -Name 'no paths returns nothing'
}

function New-Run([string]$Status, $Conclusion, [string]$Event = 'push', [string]$Url = 'https://github.com/o/r/actions/runs/1') {
    return [pscustomobject]@{ status = $Status; conclusion = $Conclusion; event = $Event; html_url = $Url }
}

function Test-TestGateVerdict {
    $sha = '0123456789abcdef0123456789abcdef01234567'
    $success = New-Run 'completed' 'success' 'push' 'https://github.com/o/r/actions/runs/42'
    $failure = New-Run 'completed' 'failure' 'pull_request' 'https://github.com/o/r/actions/runs/7'
    $running = New-Run 'in_progress' $null 'push' 'https://github.com/o/r/actions/runs/8'

    $verdict = Get-TestGateVerdict -Runs @($failure, $success) -Sha $sha
    Assert-Equal -Expected $true -Actual $verdict.Pass -Name 'a success among failures passes'
    Assert-Equal -Expected 'pass' -Actual $verdict.Reason -Name 'a passing gate has the pass reason'
    Assert-True -Condition ($verdict.Message -clike '*actions/runs/42*') -Name 'a passing message includes the run url'

    $withRunning = Get-TestGateVerdict -Runs @($failure, $running) -Sha $sha
    Assert-Equal -Expected $false -Actual $withRunning.Pass -Name 'a failure plus an in-progress run does not pass'
    Assert-Equal -Expected 'in-progress' -Actual $withRunning.Reason -Name 'a failure plus an in-progress run is in-progress'
    Assert-True -Condition ($withRunning.Message -clike '*tests still running; dispatch again when done*') -Name 'the in-progress message asks to dispatch again'

    $queued = Get-TestGateVerdict -Runs @(New-Run 'queued' $null) -Sha $sha
    Assert-Equal -Expected 'in-progress' -Actual $queued.Reason -Name 'a queued run is in-progress'

    $successBeatsRunning = Get-TestGateVerdict -Runs @($running, $success) -Sha $sha
    Assert-Equal -Expected 'pass' -Actual $successBeatsRunning.Reason -Name 'a success wins over an in-progress run'

    $failed = Get-TestGateVerdict -Runs @($failure, (New-Run 'completed' 'cancelled')) -Sha $sha
    Assert-Equal -Expected $false -Actual $failed.Pass -Name 'only failures do not pass'
    Assert-Equal -Expected 'failed' -Actual $failed.Reason -Name 'only failures are failed'
    Assert-True -Condition ($failed.Message -clike '*failure*cancelled*') -Name 'the failed message lists the conclusions'
    Assert-True -Condition ($failed.Message -clike '*not the bot commit*') -Name 'the failed message warns against the bot commit'

    foreach ($runs in @($null, @())) {
        $absent = Get-TestGateVerdict -Runs $runs -Sha $sha
        Assert-Equal -Expected $false -Actual $absent.Pass -Name 'no runs do not pass'
        Assert-Equal -Expected 'absent' -Actual $absent.Reason -Name 'no runs are absent'
        Assert-True -Condition ($absent.Message -cmatch 'Run tests') -Name 'the absent message names Run tests'
        Assert-True -Condition ($absent.Message -clike '*tested PR merge commit, not the bot commit*') -Name 'the absent message states the tagging rule'
    }

    $single = Get-TestGateVerdict -Runs $success -Sha $sha
    Assert-Equal -Expected 'pass' -Actual $single.Reason -Name 'a single run object is accepted'
}

function Test-PromotionAction {
    Assert-Equal -Expected 'unchanged' -Actual (Get-PromotionAction -Tag 'v0.9.1' -LatestTag 'v0.9.1') -Name 'the latest release being this tag is unchanged'
    Assert-Equal -Expected 'promote' -Actual (Get-PromotionAction -Tag 'v0.9.1' -LatestTag '') -Name 'no latest release promotes'
    Assert-Equal -Expected 'promote' -Actual (Get-PromotionAction -Tag 'v0.9.1' -LatestTag $null) -Name 'a null latest release promotes'
    Assert-Equal -Expected 'promote' -Actual (Get-PromotionAction -Tag 'v0.9.1' -LatestTag 'v0.9.0') -Name 'a lower latest release promotes'
    Assert-Equal -Expected 'superseded' -Actual (Get-PromotionAction -Tag 'v0.9.1' -LatestTag 'v0.10.0') -Name 'a higher latest release supersedes'
    Assert-Equal -Expected 'promote' -Actual (Get-PromotionAction -Tag 'v0.10.0' -LatestTag 'v0.9.9') -Name 'versions compare numerically, not textually'
    Assert-Throws -Action { Get-PromotionAction -Tag 'v0.9.1' -LatestTag 'v0.9.1-test.abc1234' } -MessagePattern 'not a stable release tag' -Name 'a test latest tag throws'
    Assert-Throws -Action { Get-PromotionAction -Tag 'v0.9.1' -LatestTag 'latest' } -MessagePattern 'Malformed tag' -Name 'a malformed latest tag throws'
    Assert-Throws -Action { Get-PromotionAction -Tag 'nope' -LatestTag 'v0.9.1' } -MessagePattern 'Malformed tag' -Name 'a malformed tag throws'
}

function Test-VersionRecordNeededDecision {
    Assert-Equal -Expected $true -Actual (Test-VersionRecordNeeded -MainVersion '0.9.1' -Version '0.9.2') -Name 'a higher version needs a record'
    Assert-Equal -Expected $false -Actual (Test-VersionRecordNeeded -MainVersion '0.9.2' -Version '0.9.2') -Name 'an equal version needs no record'
    Assert-Equal -Expected $false -Actual (Test-VersionRecordNeeded -MainVersion '0.10.0' -Version '0.9.2') -Name 'a lower version needs no record'
    Assert-Equal -Expected $true -Actual (Test-VersionRecordNeeded -MainVersion '0.9.9' -Version '0.10.0') -Name 'the record comparison is numeric'
    Assert-Throws -Action { Test-VersionRecordNeeded -MainVersion '0.9' -Version '0.9.2' } -MessagePattern 'Malformed version' -Name 'a malformed main version throws'
}

function Test-PublisherVersionExistsDecision {
    Assert-Equal -Expected $true -Actual (Test-PublisherVersionExists -Output 'ERROR: CodeScene.codescene-vs v0.9.2 already exists.') -Name 'an already exists message is detected'
    Assert-Equal -Expected $true -Actual (Test-PublisherVersionExists -Output 'The extension ALREADY EXISTS in the marketplace') -Name 'already exists is matched case-insensitively'
    Assert-Equal -Expected $true -Actual (Test-PublisherVersionExists -Output 'Error: Version 0.9.2 of the extension exists') -Name 'a version exists message is detected'
    Assert-Equal -Expected $true -Actual (Test-PublisherVersionExists -Output @('Publishing...', 'Failed: the version already exists')) -Name 'multi-line output is searched'
    Assert-Equal -Expected $false -Actual (Test-PublisherVersionExists -Output 'ERROR: 401 Unauthorized. Failed to authenticate with the publisher credential.') -Name 'an auth failure is not an existing version'
    Assert-Equal -Expected $false -Actual (Test-PublisherVersionExists -Output "Checking version`nThe publisher exists") -Name 'version and exists on different lines are not combined'
    Assert-Equal -Expected $false -Actual (Test-PublisherVersionExists -Output '') -Name 'empty output is not an existing version'
    Assert-Equal -Expected $false -Actual (Test-PublisherVersionExists -Output $null) -Name 'null output is not an existing version'
}

function Test-GalleryHasVersionDecision {
    $fixture = '{"results":[{"extensions":[{"extensionName":"codescene-vs","versions":[{"version":"0.9.1","flags":"validated"},{"version":"0.9.0","flags":"validated"}]}],"resultMetadata":[]}]}'
    $response = $fixture | ConvertFrom-Json
    Assert-Equal -Expected $true -Actual (Test-GalleryHasVersion -Response $response -Version '0.9.1') -Name 'a listed gallery version is found'
    Assert-Equal -Expected $true -Actual (Test-GalleryHasVersion -Response $response -Version '0.9.0') -Name 'an older listed gallery version is found'
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $response -Version '0.9.2') -Name 'an unlisted gallery version is not found'
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $response -Version '0.9') -Name 'a version prefix is not a match'

    $none = '{"results":[{"extensions":[],"resultMetadata":[]}]}' | ConvertFrom-Json
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $none -Version '0.9.1') -Name 'no gallery extensions has no version'

    $noVersions = '{"results":[{"extensions":[{"extensionName":"codescene-vs"}]}]}' | ConvertFrom-Json
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $noVersions -Version '0.9.1') -Name 'an extension without versions has no version'

    $noResults = '{"results":[]}' | ConvertFrom-Json
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $noResults -Version '0.9.1') -Name 'no gallery results has no version'
    Assert-Equal -Expected $false -Actual (Test-GalleryHasVersion -Response $null -Version '0.9.1') -Name 'a null gallery response has no version'
}

function Test-MarketplaceOverview {
    $baseUrl = 'https://raw.githubusercontent.com/codescene-oss/codescene-vs/main'
    $readme = "<img src=`"screenshots/a.png`" />`n![b](screenshots/b.png)`n[docs](https://codescene.io/docs)`n[local](docs/screenshots/c.md)`n[shot](screenshots-other/d.png)"
    $overview = ConvertTo-MarketplaceOverview -Readme $readme
    $expected = "<img src=`"$baseUrl/screenshots/a.png`" />`n![b]($baseUrl/screenshots/b.png)`n[docs](https://codescene.io/docs)`n[local](docs/screenshots/c.md)`n[shot](screenshots-other/d.png)"
    Assert-Equal -Expected $expected -Actual $overview -Name 'both image syntaxes are rewritten and other links are untouched'
    Assert-Equal -Expected 'plain text' -Actual (ConvertTo-MarketplaceOverview -Readme 'plain text') -Name 'text without screenshots is unchanged'
    Assert-Equal -Expected $overview -Actual (ConvertTo-MarketplaceOverview -Readme $overview) -Name 'rewriting is idempotent'
}
