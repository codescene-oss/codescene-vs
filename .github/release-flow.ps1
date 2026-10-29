Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'release-flow.psm1') -Force

$script:Commands = @('metadata', 'set-version', 'verify-vsix', 'create-release', 'upload', 'download-asset', 'test-gate', 'promote', 'write-overview', 'publish', 'record-version')
$script:DefaultRepository = 'codescene-oss/codescene-vs'

function Get-UsageMessage([string]$Command) {
    return "Unknown command '$Command'. Usage: release-flow.ps1 <$($script:Commands -join '|')>"
}

function Get-RequiredEnvironment([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Environment variable $Name is required."
    }
    return $value
}

function Get-OptionalEnvironment([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ($null -eq $value) { return '' }
    return $value
}

function Assert-GitHubToken {
    if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN) -and [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        throw 'GH_TOKEN is required to call GitHub.'
    }
}

function Initialize-GitHubCommand {
    Assert-GitHubToken
    $env:GH_REPO = Get-GitHubRepository
}

function Resolve-RepositoryPath {
    $path = Get-OptionalEnvironment 'REPO_PATH'
    if ([string]::IsNullOrWhiteSpace($path)) {
        $path = (Get-Location).ProviderPath
    }
    return (Resolve-Path -LiteralPath $path).ProviderPath
}

function Get-GitHubRepository {
    $repository = Get-OptionalEnvironment 'GITHUB_REPOSITORY'
    if ([string]::IsNullOrWhiteSpace($repository)) { return $script:DefaultRepository }
    return $repository
}

function Test-TagExists([string]$Tag) {
    return (Invoke-Native -FilePath 'git' -ArgumentList @('rev-parse', '-q', '--verify', "refs/tags/$Tag")).ExitCode -eq 0
}

function Get-OtherStableReleases($Releases, [string]$Tag) {
    return @(Select-PublishedStableRelease -Releases $Releases -ExcludeTag $Tag)
}

function Invoke-Metadata {
    Initialize-GitHubCommand
    $tag = Get-RequiredEnvironment 'TAG'
    $eventName = Get-RequiredEnvironment 'EVENT'
    $runRef = Get-OptionalEnvironment 'RUN_REF'
    $dryRun = (Get-OptionalEnvironment 'DRY_RUN') -ceq 'true'

    $info = Get-ReleaseTagInfo -Tag $tag
    Write-Host "Tag $tag is a $($info.Kind) tag for version $($info.Version) (event: $eventName, dry run: $dryRun)"

    if ($eventName -ceq 'workflow_dispatch') {
        Assert-DispatchAllowed -EventName $eventName -RunRef $runRef -TagExists (Test-TagExists $tag) -DryRun $dryRun -Kind $info.Kind
    }

    $commit = Get-TagCommit -Tag $tag
    if (-not $commit) {
        throw "Tag $tag does not resolve to a commit in this checkout."
    }
    Write-Host "Tagged commit: $commit"

    $onMain = Test-GitAncestor -Ancestor $commit -Descendant 'origin/main'
    Write-Host "Tagged commit is reachable from origin/main: $onMain"

    $releases = @(Get-GitHubRelease -Repository (Get-GitHubRepository))
    $releaseExists = @($releases | Where-Object { -not $_.draft -and $_.tag_name -ceq $tag }).Count -gt 0
    Write-Host "A published release for $tag exists: $releaseExists"

    $mode = Get-ComparisonMode -Kind $info.Kind -EventName $eventName -ReleaseExists $releaseExists
    Write-Host "Comparison mode: $mode"

    $others = Get-OtherStableReleases $releases $tag
    $highest = Select-HighestStableRelease -Releases $others
    $isAncestor = { param($Candidate) Test-GitAncestor -Ancestor $Candidate -Descendant $commit }.GetNewClosure()
    $previous = Select-PreviousStableRelease -Releases $others -IsAncestor $isAncestor
    $highestIsAncestor = $null
    if ($null -ne $highest) {
        $highestIsAncestor = & $isAncestor $highest.tag_name
        Write-Host "Highest stable release: $($highest.tag_name) (ancestor of the tagged commit: $highestIsAncestor)"
    }
    else {
        Write-Host 'No other published stable release exists.'
    }

    $notesStart = ''
    if ($null -ne $previous) {
        $notesStart = $previous.tag_name
        Write-Host "Previous stable release: $notesStart"
    }
    else {
        Write-Host 'No previous stable release is an ancestor of the tagged commit.'
    }

    $facts = New-TagAdmissionFacts -OnMain $onMain -Highest $highest -HighestIsAncestor $highestIsAncestor -Previous $previous -TaggedCommit $commit
    Assert-TagAdmissible -TagInfo $info -Mode $mode -Facts $facts

    Write-ActionOutput -Name 'tag' -Value $tag
    Write-ActionOutput -Name 'version' -Value $info.Version
    Write-ActionOutput -Name 'kind' -Value $info.Kind
    Write-ActionOutput -Name 'notes_start' -Value $notesStart
    Write-ActionOutput -Name 'sha' -Value $commit
    Write-ActionOutput -Name 'dry_run' -Value $(if ($dryRun) { 'true' } else { 'false' })
}

function Invoke-CreateRelease {
    Initialize-GitHubCommand
    $tag = Get-RequiredEnvironment 'TAG'
    $kind = Get-RequiredEnvironment 'KIND'
    $notesStart = Get-OptionalEnvironment 'NOTES_START'
    $repository = Get-GitHubRepository

    $info = Get-ReleaseTagInfo -Tag $tag
    if ($info.Kind -cne $kind) {
        throw "Kind '$kind' does not match tag $tag, which is a $($info.Kind) tag."
    }
    $sha = Get-OptionalEnvironment 'SHA'
    $serverUrl = Get-OptionalEnvironment 'GITHUB_SERVER_URL'
    if ($kind -ceq 'test') {
        $sha = Get-RequiredEnvironment 'SHA'
        $serverUrl = Get-RequiredEnvironment 'GITHUB_SERVER_URL'
    }

    $generated = Get-GeneratedReleaseNotes -Repository $repository -Tag $tag -PreviousTag $notesStart
    Write-Host "Generated release notes for $tag (changes since '$notesStart')"
    $body = Get-ReleaseNotesBody -Kind $kind -Generated $generated -Sha $sha -ServerUrl $serverUrl -Repository $repository
    $notesFile = Write-ReleaseNotesFile -Tag $tag -Body $body

    $exists = Test-ReleaseExists -Tag $tag
    $isLatest = $false
    if ($exists -and $kind -ceq 'stable') {
        $isLatest = (Get-LatestReleaseTag -Repository $repository) -ceq $tag
    }
    Write-Host "Release exists: $exists, keeps the latest marker: $isLatest"

    $arguments = Get-ReleaseCommandArguments -Exists $exists -Kind $kind -Tag $tag -NotesFile $notesFile -IsLatest $isLatest
    Invoke-NativeChecked -FilePath 'gh' -ArgumentList (@('release') + $arguments) | Out-Null
    Write-Host "Release $tag $(if ($exists) { 'updated' } else { 'created' })"
}

function Get-VsixFileInDirectory([string]$Directory) {
    $files = @(Get-ChildItem -LiteralPath $Directory -File | Where-Object { $_.Name -like '*.vsix' })
    if ($files.Count -eq 0) {
        throw "No .vsix file found in $Directory."
    }
    return @($files | ForEach-Object { $_.FullName })
}

function Invoke-Upload {
    Initialize-GitHubCommand
    $tag = Get-RequiredEnvironment 'TAG'
    $directory = Get-RequiredEnvironment 'ASSET_DIR'

    $files = @(Get-VsixFileInDirectory $directory)
    $attached = @(Get-ReleaseAssetName -Tag $tag)
    $missing = @(Select-MissingAsset -ExistingNames $attached -Paths $files)
    foreach ($file in $files) {
        if ($missing -cnotcontains $file) {
            Write-Host "Skipping $([IO.Path]::GetFileName($file)): already attached to $tag"
        }
    }
    foreach ($file in $missing) {
        Invoke-NativeChecked -FilePath 'gh' -ArgumentList @('release', 'upload', $tag, $file) | Out-Null
        Write-Host "Uploaded $([IO.Path]::GetFileName($file)) to $tag"
    }
}

function Invoke-DownloadAsset {
    Initialize-GitHubCommand
    $tag = Get-RequiredEnvironment 'TAG'
    $directory = Get-RequiredEnvironment 'ASSET_DIR'

    Invoke-NativeChecked -FilePath 'gh' -ArgumentList (Get-AssetDownloadArguments -Tag $tag -Directory $directory) | Out-Null
    $vsix = Find-SingleVsix -Root $directory
    Write-Host "Downloaded $vsix from $tag"
}

function Invoke-TestGate {
    Initialize-GitHubCommand
    $sha = Get-RequiredEnvironment 'SHA'

    $runs = @(Get-TestRun -Repository (Get-GitHubRepository) -Sha $sha)
    $verdict = Get-TestGateVerdict -Runs $runs -Sha $sha
    if (-not $verdict.Pass) {
        throw $verdict.Message
    }
    Write-Host $verdict.Message
}

function Invoke-Promote {
    Initialize-GitHubCommand
    $tag = Get-RequiredEnvironment 'TAG'

    if ((Get-ReleaseTagInfo -Tag $tag).Kind -cne 'stable') {
        throw "Only a stable release can be promoted, but $tag is not one."
    }
    $latest = Get-LatestReleaseTag -Repository (Get-GitHubRepository)
    $action = Get-PromotionAction -Tag $tag -LatestTag $latest
    switch -CaseSensitive ($action) {
        'promote' {
            Invoke-NativeChecked -FilePath 'gh' -ArgumentList @('release', 'edit', $tag, '--latest') | Out-Null
            Write-Host "Release $tag is now the latest release"
        }
        'unchanged' { Write-Host "Release $tag is already the latest release" }
        'superseded' { Write-Host "Release $tag is superseded by $latest, which stays the latest release" }
        default { throw "Unknown promotion action '$action'." }
    }
}

function Invoke-SetVersion([string]$RepositoryPath) {
    $version = Get-RequiredEnvironment 'VERSION'
    Set-ReleaseVersion -RepositoryPath $RepositoryPath -Version $version
    Write-Host "Version set to $version"
}

function Invoke-VerifyVsix([string]$RepositoryPath) {
    $version = Get-RequiredEnvironment 'VERSION'
    $root = Join-Path $RepositoryPath 'Codescene.VSExtension.VS2022'
    $vsix = Find-SingleVsix -Root $root
    Assert-VsixVersion -Path $vsix -Version $version
    Write-Host "VSIX $vsix embeds version $version"
    Write-ActionOutput -Name 'path' -Value $vsix
}

function Invoke-WriteOverview([string]$RepositoryPath) {
    $path = Write-MarketplaceOverview -RepositoryPath $RepositoryPath
    Write-Host "Wrote $path"
}

function Invoke-Publish {
    $version = Get-RequiredEnvironment 'VERSION'
    $publisher = Get-RequiredEnvironment 'VSIX_PUBLISHER'
    $directory = Get-RequiredEnvironment 'VSIX_DIR'
    $manifest = Get-RequiredEnvironment 'PUBLISH_MANIFEST'

    Invoke-MarketplacePublish -Version $version -PublisherPath $publisher -VsixDirectory $directory -PublishManifest $manifest | Out-Null
}

function Invoke-RecordVersion([string]$RepositoryPath) {
    $version = Get-RequiredEnvironment 'VERSION'
    Invoke-VersionRecord -RepositoryPath $RepositoryPath -Version $version | Out-Null
}

function Invoke-ReleaseCommand([string]$Command, [string]$RepositoryPath) {
    switch -CaseSensitive ($Command) {
        'metadata' { Invoke-Metadata }
        'set-version' { Invoke-SetVersion $RepositoryPath }
        'verify-vsix' { Invoke-VerifyVsix $RepositoryPath }
        'create-release' { Invoke-CreateRelease }
        'upload' { Invoke-Upload }
        'download-asset' { Invoke-DownloadAsset }
        'test-gate' { Invoke-TestGate }
        'promote' { Invoke-Promote }
        'write-overview' { Invoke-WriteOverview $RepositoryPath }
        'publish' { Invoke-Publish }
        'record-version' { Invoke-RecordVersion $RepositoryPath }
        default { throw (Get-UsageMessage $Command) }
    }
}

function Main([string]$Command) {
    if ($script:Commands -cnotcontains $Command) {
        throw (Get-UsageMessage $Command)
    }
    $repositoryPath = Resolve-RepositoryPath
    Set-Location -LiteralPath $repositoryPath
    Invoke-ReleaseCommand $Command $repositoryPath
}

$command = ''
if ($args.Count -gt 0) { $command = [string]$args[0] }
Main $command
