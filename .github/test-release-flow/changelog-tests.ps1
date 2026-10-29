$script:ChangelogFixture = @'
# Changelog

Text before the first section is ignored.

## [Unreleased]

## [0.9.1] - 2026-10-28
### Fixed
- keep WebView2 visible (#341)

## [0.9.0]
### Added
- run cs-ide as a persistent server
### Changed
- bump the CLI

## [0.8.7]

## [0.8.1] - 2026-09-07
### Fixed
- a draft only fix

## Notes
Plain notes
'@

$script:ChangelogFixture = $script:ChangelogFixture.Replace("`r`n", "`n")

function Get-ChangelogSectionBody($Sections, [string]$Key) {
    return @($Sections | Where-Object { $_.Key -ceq $Key })[0].Body
}

function Test-ChangelogParsing {
    $sections = @(ConvertFrom-Changelog -Text $script:ChangelogFixture)
    Assert-Equal -Expected 'Unreleased,0.9.1,0.9.0,0.8.7,0.8.1,Notes' -Actual (($sections | ForEach-Object { $_.Key }) -join ',') -Name 'changelog section keys come from the headings in order'
    Assert-Equal -Expected '' -Actual (Get-ChangelogSectionBody $sections 'Unreleased') -Name 'an empty section has an empty body'
    Assert-Equal -Expected "### Fixed`n- keep WebView2 visible (#341)" -Actual (Get-ChangelogSectionBody $sections '0.9.1') -Name 'a dated section keeps its subsections and loses surrounding blank lines'
    Assert-Equal -Expected "### Added`n- run cs-ide as a persistent server`n### Changed`n- bump the CLI" -Actual (Get-ChangelogSectionBody $sections '0.9.0') -Name 'an undated section keeps all its subsections'
    Assert-Equal -Expected '' -Actual (Get-ChangelogSectionBody $sections '0.8.7') -Name 'an empty versioned section has an empty body'
    Assert-Equal -Expected 'Plain notes' -Actual (Get-ChangelogSectionBody $sections 'Notes') -Name 'a plain heading uses the raw heading as its key'
    Assert-True -Condition (-not (($sections | ForEach-Object { $_.Body }) -join "`n").Contains('Text before')) -Name 'text before the first section is ignored'

    $crlf = @(ConvertFrom-Changelog -Text ($script:ChangelogFixture -replace "`n", "`r`n"))
    Assert-Equal -Expected (($sections | ForEach-Object { $_.Key + '=' + $_.Body }) -join '|') -Actual (($crlf | ForEach-Object { $_.Key + '=' + $_.Body }) -join '|') -Name 'a CRLF changelog parses like an LF one'

    Assert-Equal -Expected 0 -Actual @(ConvertFrom-Changelog -Text '').Count -Name 'an empty changelog has no sections'
    Assert-Equal -Expected 0 -Actual @(ConvertFrom-Changelog -Text "# Title`nno sections here").Count -Name 'a changelog without sections has none'
    Assert-Equal -Expected 0 -Actual @(ConvertFrom-Changelog -Text "### Added`n- not a section").Count -Name 'a third-level heading is not a section'
    Assert-Equal -Expected 'Notes' -Actual (@(ConvertFrom-Changelog -Text '## Notes')[0].Key) -Name 'a heading on the last line without a newline is a section'
    Assert-Equal -Expected '[1.0.0]x' -Actual (@(ConvertFrom-Changelog -Text "## [1.0.0]x`nbody")[0].Key) -Name 'a bracket heading with trailing text is a raw key'

    Assert-Throws -Action { ConvertFrom-Changelog -Text "## [0.9.1]`n- a`n## [0.9.1] - 2026-10-28`n- b" } -MessagePattern "Duplicate changelog section '0\.9\.1'" -Name 'a duplicate version key throws'
    Assert-Throws -Action { ConvertFrom-Changelog -Text "## Notes`n## Notes" } -MessagePattern "Duplicate changelog section 'Notes'" -Name 'a duplicate raw key throws'
}

function Test-ChangelogSectionTag {
    Assert-Equal -Expected 'v0.9.1' -Actual (Get-ChangelogSectionTag -Key '0.9.1') -Name '0.9.1 maps to v0.9.1'
    Assert-Equal -Expected 'v10.20.30' -Actual (Get-ChangelogSectionTag -Key '10.20.30') -Name '10.20.30 maps to v10.20.30'
    foreach ($key in @('Unreleased', 'Notes', '0.9', '1.2.3.4', '01.2.3', 'v0.9.1', '0.9.1-test', ' 0.9.1', '')) {
        Assert-True -Condition ($null -eq (Get-ChangelogSectionTag -Key $key)) -Name "section '$key' matches no tag"
    }
}

function Get-ExpectedChangelogBlock([string]$Section) {
    return "<!-- changelog-migration:start -->`n## Changelog`n`n$Section`n<!-- changelog-migration:end -->`n"
}

function Test-ChangelogBlock {
    $section = "### Fixed`n- a fix"
    $block = Get-ExpectedChangelogBlock $section
    Assert-Equal -Expected $block -Actual (Set-ChangelogBlock -Body '' -Section $section) -Name 'an empty body becomes the block'
    Assert-Equal -Expected $block -Actual (Set-ChangelogBlock -Body $null -Section $section) -Name 'a null body becomes the block'
    Assert-Equal -Expected $block -Actual (Set-ChangelogBlock -Body "  `n" -Section $section) -Name 'a blank body becomes the block'

    $automated = Set-ChangelogBlock -Body 'Automated build for v0.9.1' -Section $section
    Assert-Equal -Expected "Automated build for v0.9.1`n`n$block" -Actual $automated -Name 'the block follows a blank line after existing text'
    Assert-Equal -Expected "Automated build for v0.9.1`n`n$block" -Actual (Set-ChangelogBlock -Body "Automated build for v0.9.1`n`n`n" -Section $section) -Name 'trailing blank lines before the block collapse to one blank line'

    Assert-Equal -Expected $automated -Actual (Set-ChangelogBlock -Body $automated -Section $section) -Name 'applying the same section twice is idempotent'
    $replaced = Set-ChangelogBlock -Body $automated -Section "### Changed`n- other"
    Assert-Equal -Expected "Automated build for v0.9.1`n`n$(Get-ExpectedChangelogBlock "### Changed`n- other")" -Actual $replaced -Name 'an existing block is replaced by the new one'
    Assert-Equal -Expected 1 -Actual ([regex]::Matches($replaced, 'changelog-migration:start')).Count -Name 'a replaced block is not duplicated'
    Assert-Equal -Expected $block -Actual (Set-ChangelogBlock -Body $block -Section $section) -Name 'a body that is only the block stays the block'

    $crlf = Set-ChangelogBlock -Body "Automated build`r`n`r`nmore text`r`n" -Section "### Fixed`r`n- a fix"
    Assert-Equal -Expected "Automated build`n`nmore text`n`n$block" -Actual $crlf -Name 'a CRLF body and section are normalised to LF'
    Assert-Equal -Expected $crlf -Actual (Set-ChangelogBlock -Body ($crlf -replace "`n", "`r`n") -Section $section) -Name 'a CRLF copy of a migrated body is recognised and rewritten identically'

    $surrounded = Set-ChangelogBlock -Body "Intro`n`n$block`nAfter" -Section $section
    Assert-Equal -Expected "Intro`n`n`nAfter`n`n$block" -Actual $surrounded -Name 'text around an existing block is kept and the block moves to the end'
    Assert-Equal -Expected $surrounded -Actual (Set-ChangelogBlock -Body $surrounded -Section $section) -Name 'a moved block is stable on the next application'
}

function Get-FixtureRelease([string]$Tag, [string]$Body, [bool]$Draft, [bool]$Prerelease, [int]$Id) {
    return [pscustomobject]@{ tag_name = $Tag; draft = $Draft; prerelease = $Prerelease; id = $Id; body = $Body }
}

function Get-MigrationFixtureReleases([string]$Published090Body) {
    return @(
        (Get-FixtureRelease 'v0.9.1' 'Automated build for v0.9.1' $false $false 101),
        (Get-FixtureRelease 'v0.8.1' 'Automated build for v0.8.1' $true $false 81),
        (Get-FixtureRelease 'v0.9.0-test.abc1234' 'Test build' $false $true 90),
        (Get-FixtureRelease 'v0.9.0' $Published090Body $false $false 100),
        (Get-FixtureRelease 'v0.8.7' 'Automated build for v0.8.7' $false $false 87)
    )
}

function Test-ChangelogMigrationPlan {
    $sections = @(ConvertFrom-Changelog -Text $script:ChangelogFixture)
    $section090 = Get-ChangelogSectionBody $sections '0.9.0'
    $section091 = Get-ChangelogSectionBody $sections '0.9.1'
    $identical = Set-ChangelogBlock -Body 'Automated build for v0.9.0' -Section $section090
    $plan = Get-ChangelogMigrationPlan -Sections $sections -Releases (Get-MigrationFixtureReleases $identical)

    Assert-Equal -Expected 'v0.9.1' -Actual (@($plan.Updates | ForEach-Object { $_.Tag }) -join ',') -Name 'only the release without the block is updated'
    Assert-Equal -Expected 101 -Actual $plan.Updates[0].Id -Name 'the update carries the release id'
    Assert-Equal -Expected (Set-ChangelogBlock -Body 'Automated build for v0.9.1' -Section $section091) -Actual $plan.Updates[0].Body -Name 'the update carries the new body'
    Assert-Equal -Expected (Get-ExpectedChangelogBlock $section091) -Actual $plan.Updates[0].Block -Name 'the update carries the block for display'
    Assert-Equal -Expected 'v0.9.0' -Actual ($plan.UpToDate -join ',') -Name 'a release already carrying the identical block is up to date'
    Assert-Equal -Expected '0.8.7' -Actual ($plan.Empty -join ',') -Name 'an empty section of a published release is reported as empty'
    Assert-Equal -Expected 'Unreleased,0.8.1,Notes' -Actual ($plan.Unmatched -join ',') -Name 'sections without a published stable release are unmatched, including the draft'

    $crlfPlan = Get-ChangelogMigrationPlan -Sections $sections -Releases (Get-MigrationFixtureReleases ($identical -replace "`n", "`r`n"))
    Assert-Equal -Expected 'v0.9.0' -Actual ($crlfPlan.UpToDate -join ',') -Name 'a CRLF current body compares equal after normalisation'
    Assert-Equal -Expected 'v0.9.1' -Actual (@($crlfPlan.Updates | ForEach-Object { $_.Tag }) -join ',') -Name 'a CRLF current body adds no updates'

    $stale = Get-ChangelogMigrationPlan -Sections $sections -Releases (Get-MigrationFixtureReleases (Set-ChangelogBlock -Body 'Automated build for v0.9.0' -Section 'old text'))
    Assert-Equal -Expected 'v0.9.1,v0.9.0' -Actual (@($stale.Updates | ForEach-Object { $_.Tag }) -join ',') -Name 'a release with a different block is updated'
    Assert-Equal -Expected 0 -Actual $stale.UpToDate.Count -Name 'nothing is up to date when the block differs'

    $blank = Get-ChangelogMigrationPlan -Sections $sections -Releases (Get-MigrationFixtureReleases '')
    Assert-Equal -Expected (Get-ExpectedChangelogBlock $section090) -Actual @($blank.Updates | Where-Object { $_.Tag -ceq 'v0.9.0' })[0].Body -Name 'a release with an empty body gets only the block'

    $unpublished = Get-ChangelogMigrationPlan -Sections $sections -Releases @((Get-FixtureRelease 'v0.9.1' '' $false $false 1))
    Assert-Equal -Expected 'Unreleased,0.9.0,0.8.7,0.8.1,Notes' -Actual ($unpublished.Unmatched -join ',') -Name 'an empty section without a release is unmatched, not empty'
    Assert-Equal -Expected 0 -Actual $unpublished.Empty.Count -Name 'unmatched is checked before empty'

    $none = Get-ChangelogMigrationPlan -Sections @() -Releases @()
    Assert-Equal -Expected '0,0,0,0' -Actual (@($none.Updates.Count, $none.UpToDate.Count, $none.Empty.Count, $none.Unmatched.Count) -join ',') -Name 'no sections and no releases give an empty plan'
    $nullInputs = Get-ChangelogMigrationPlan -Sections $null -Releases $null
    Assert-Equal -Expected 0 -Actual $nullInputs.Updates.Count -Name 'null inputs give an empty plan'
}

function Invoke-HistoryGit([string]$Directory, [string[]]$Arguments) {
    return (Invoke-NativeChecked -FilePath 'git' -ArgumentList (@('-C', $Directory, '-c', 'user.name=release-flow-test', '-c', 'user.email=release-flow-test@example.com') + $Arguments)).Output
}

function New-HistoryRepository {
    $root = New-TempDirectory
    Invoke-HistoryGit $root @('init', '--quiet', '--initial-branch=main') | Out-Null
    return $root
}

function Test-ReadFileFromHistory {
    $hint = 'history-fixture\.md deletion not found in git history; pass --file <path>'
    $repository = New-HistoryRepository
    $first = "# Fixture`n`n## [0.1.0]`n- caf`u{00E9} first version"
    Write-AllText (Join-Path $repository 'history-fixture.md') "$first`n"
    Invoke-HistoryGit $repository @('add', 'history-fixture.md') | Out-Null
    Invoke-HistoryGit $repository @('commit', '--quiet', '-m', 'add fixture') | Out-Null
    Write-AllText (Join-Path $repository 'history-fixture.md') "# Fixture`n`nedited`n"
    Invoke-HistoryGit $repository @('commit', '--quiet', '-am', 'edit fixture') | Out-Null
    Assert-Throws -Action { Read-FileFromHistory -RepositoryPath $repository -Path 'history-fixture.md' } -MessagePattern $hint -Name 'a file that still exists has no deletion to read'

    Invoke-HistoryGit $repository @('rm', '--quiet', 'history-fixture.md') | Out-Null
    Invoke-HistoryGit $repository @('commit', '--quiet', '-m', 'delete fixture') | Out-Null
    Assert-Equal -Expected "# Fixture`n`nedited" -Actual (Read-FileFromHistory -RepositoryPath $repository -Path 'history-fixture.md') -Name 'the content before the deletion commit is returned'
    Assert-Throws -Action { Read-FileFromHistory -RepositoryPath $repository -Path 'never-existed.md' } -MessagePattern 'never-existed\.md deletion not found in git history; pass --file <path>' -Name 'a file that never existed throws the hint'

    $other = New-HistoryRepository
    Write-AllText (Join-Path $other 'other.md') 'x'
    Invoke-HistoryGit $other @('add', 'other.md') | Out-Null
    Invoke-HistoryGit $other @('commit', '--quiet', '-m', 'only an addition') | Out-Null
    Assert-Throws -Action { Read-FileFromHistory -RepositoryPath $other -Path 'history-fixture.md' } -MessagePattern $hint -Name 'a repository without a deletion throws the hint'

    $unicode = New-HistoryRepository
    Write-AllText (Join-Path $unicode 'history-fixture.md') "$first`n"
    Invoke-HistoryGit $unicode @('add', 'history-fixture.md') | Out-Null
    Invoke-HistoryGit $unicode @('commit', '--quiet', '-m', 'add fixture') | Out-Null
    Invoke-HistoryGit $unicode @('rm', '--quiet', 'history-fixture.md') | Out-Null
    Invoke-HistoryGit $unicode @('commit', '--quiet', '-m', 'delete fixture') | Out-Null
    Assert-Equal -Expected $first -Actual (Read-FileFromHistory -RepositoryPath $unicode -Path 'history-fixture.md') -Name 'the content from history is the first commit and keeps non-ASCII text'

    Push-Location -LiteralPath $unicode
    try {
        Assert-Equal -Expected $first -Actual (Read-FileFromHistory -Path 'history-fixture.md') -Name 'the repository defaults to the current directory'
    }
    finally {
        Pop-Location
    }
}
