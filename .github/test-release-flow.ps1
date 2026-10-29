Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'release-flow.psm1') -Force

$script:GitHubDirectory = $PSScriptRoot
$script:RepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:Passed = 0
$script:TempPaths = @()
$script:LocalTags = @()

$script:TestFiles = @(
    'harness', 'tag-tests', 'selection-tests', 'admission-tests',
    'version-file-tests', 'release-decision-tests', 'process-tests', 'entry-script-tests',
    'metadata-tests', 'version-command-tests', 'github-command-tests', 'release-command-tests',
    'tag-script-tests', 'marketplace-tests', 'version-record-tests', 'publish-command-tests',
    'changelog-tests', 'migration-script-tests'
)

foreach ($testFile in $script:TestFiles) {
    . (Join-Path $PSScriptRoot "test-release-flow\$testFile.ps1")
}

try {
    Test-ReleaseTagInfo
    Test-CompareReleaseVersion
    Test-NextReleaseVersion
    Test-TestReleaseTagName
    Test-ReleaseSelection
    Test-ComparisonMode
    Test-DispatchAllowed
    Test-TagAdmissible
    Test-VersionFilePaths
    Test-ReleaseVersionFiles
    Test-ReleaseVersionPreservesEncoding
    Test-ReleaseVersionFailures
    Test-VsixVerification
    Test-FindSingleVsix
    Test-BuiltVsixIntegration
    Test-Utf8ConsoleEncoding
    Test-InvokeNative
    Test-WriteActionOutput
    Test-GitHelpers
    Test-EntryScriptUsage
    Test-MetadataIntegration
    Test-SetVersionIntegration
    Test-VerifyVsixIntegration
    Test-HelperIndependentOfTaggedTree
    Test-AssetDownloadArguments
    Test-GenerateNotesArguments
    Test-LatestReleaseTag
    Test-TestRunQuery
    Test-ReleaseNotesFile
    Test-CreateReleaseCommand
    Test-UploadCommand
    Test-DownloadAssetCommand
    Test-TestGateCommand
    Test-PromoteCommand
    Test-GitHubReadIntegration
    Test-TagTestReleaseScriptPrerequisites
    Test-TagTestReleaseScriptReleases
    Test-TagTestReleaseScriptTagging
    Test-TagTestReleaseScriptIntegration
    Test-TestReleaseNotes
    Test-ReleaseNotesConfig
    Test-ReleaseCommandArguments
    Test-MissingAssets
    Test-TestGateVerdict
    Test-PromotionAction
    Test-VersionRecordNeededDecision
    Test-PublisherVersionExistsDecision
    Test-GalleryHasVersionDecision
    Test-MarketplaceOverview
    Test-GalleryRequest
    Test-MarketplaceVersionListedSender
    Test-OnlyVersionFilesChanged
    Test-MarketplacePublishDecisions
    Test-VersionRecordIntegration
    Test-VersionRecordSyncsWithOrigin
    Test-VersionRecordStrayFile
    Test-VersionRecordRetry
    Test-RecordVersionCommand
    Test-WriteOverviewCommand
    Test-WriteOverviewOnArchive
    Test-PublishCommand
    Test-MarketplaceReadIntegration
    Test-ChangelogParsing
    Test-ChangelogSectionTag
    Test-ChangelogBlock
    Test-ChangelogMigrationPlan
    Test-ReadFileFromHistory
    Test-MigrationScriptArguments
    Test-MigrationScriptWithFakeGh
    Test-MigrationIntegration
    Write-Host "$($script:Passed) assertions passed"
}
finally {
    Remove-LocalTags
    foreach ($path in $script:TempPaths) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}
