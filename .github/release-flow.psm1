Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:PartNames = @(
    'version-tags', 'release-selection', 'dispatch-rules', 'tag-admission',
    'version-files', 'vsix', 'release-notes', 'generated-notes',
    'release-assets', 'release-commands', 'test-gate', 'promotion',
    'json-helpers', 'marketplace-gallery', 'marketplace-overview', 'marketplace-publish',
    'native-process', 'action-output', 'github-releases', 'git-helpers',
    'version-record', 'changelog'
)

foreach ($partName in $script:PartNames) {
    . (Join-Path $PSScriptRoot "release-flow\$partName.ps1")
}

Set-Utf8ConsoleEncoding

Export-ModuleMember -Function Get-ReleaseTagInfo, Compare-ReleaseVersion, Get-NextReleaseVersion, Get-TestReleaseTagName, Select-PublishedStableRelease, Select-HighestStableRelease, Select-PreviousStableRelease, Get-ComparisonMode, Assert-DispatchAllowed, Assert-TagAdmissible, New-TagAdmissionFacts, Get-VersionFilePaths, Get-ReleaseVersion, Set-ReleaseVersion, Find-SingleVsix, Get-VsixManifestVersion, Assert-VsixVersion, Format-TestReleaseNotes, Get-ReleaseNotesBody, Get-ReleaseCommandArguments, Select-MissingAsset, Get-TestGateVerdict, Get-PromotionAction, Test-VersionRecordNeeded, Test-PublisherVersionExists, Test-GalleryHasVersion, ConvertTo-MarketplaceOverview, Invoke-Native, Invoke-NativeChecked, Write-ActionOutput, Get-GitHubRelease, Get-TagCommit, Test-GitAncestor, Get-AssetDownloadArguments, Get-GenerateNotesArguments, Get-GeneratedReleaseNotes, Write-ReleaseNotesFile, Test-ReleaseExists, Get-ReleaseAssetName, Get-LatestReleaseTag, Get-TestRun, Write-MarketplaceOverview, Get-GalleryRequest, Test-MarketplaceVersionListed, Invoke-MarketplacePublish, Assert-OnlyVersionFilesChanged, Invoke-VersionRecord, ConvertFrom-Changelog, Get-ChangelogSectionTag, Set-ChangelogBlock, Get-ChangelogMigrationPlan, Read-FileFromHistory
