function New-Release([string]$Tag, [bool]$Draft = $false, [bool]$Prerelease = $false, [int]$Id = 1) {
    return [pscustomobject]@{ tag_name = $Tag; draft = $Draft; prerelease = $Prerelease; id = $Id; body = '' }
}

function Get-TagNames($Releases) {
    return (@($Releases | ForEach-Object { $_.tag_name }) -join ',')
}

function Test-ReleaseSelection {
    $releases = @(
        (New-Release 'v0.8.7'),
        (New-Release 'v0.8.1' $true),
        (New-Release 'v0.9.0'),
        (New-Release 'v0.9.0-test.015c6a0' $false $true),
        (New-Release 'v0.9.1-test.abc1234' $false $false),
        (New-Release 'not-a-tag'),
        (New-Release 'v0.10.0'),
        (New-Release 'v0.9.2' $false $true),
        (New-Release 'v0.9.1')
    )
    Assert-Equal -Expected 'v0.8.7,v0.9.0,v0.10.0,v0.9.1' -Actual (Get-TagNames (Select-PublishedStableRelease -Releases $releases -ExcludeTag '')) -Name 'published stable skips draft, prerelease, test-shaped and malformed'
    Assert-Equal -Expected 'v0.8.7,v0.9.0,v0.10.0' -Actual (Get-TagNames (Select-PublishedStableRelease -Releases $releases -ExcludeTag 'v0.9.1')) -Name 'published stable skips the excluded tag'
    Assert-Equal -Expected 'v0.8.7,v0.9.0,v0.10.0,v0.9.1' -Actual (Get-TagNames (Select-PublishedStableRelease -Releases $releases -ExcludeTag 'v9.9.9')) -Name 'excluding an unknown tag changes nothing'
    Assert-Equal -Expected 0 -Actual @(Select-PublishedStableRelease -Releases @() -ExcludeTag '').Count -Name 'published stable of an empty list is empty'
    Assert-Equal -Expected 0 -Actual @(Select-PublishedStableRelease -Releases $null -ExcludeTag '').Count -Name 'published stable of null is empty'
    Assert-Equal -Expected 1 -Actual @(Select-PublishedStableRelease -Releases (New-Release 'v0.9.0') -ExcludeTag '').Count -Name 'published stable accepts a single release'

    Assert-Equal -Expected 'v0.10.0' -Actual (Select-HighestStableRelease -Releases @((New-Release 'v0.8.7'), (New-Release 'v0.9.0'), (New-Release 'v0.10.0'))).tag_name -Name 'highest compares numerically'
    Assert-Equal -Expected 'v0.10.0' -Actual (Select-HighestStableRelease -Releases @((New-Release 'v0.10.0'), (New-Release 'v0.9.0'), (New-Release 'v0.8.7'))).tag_name -Name 'highest ignores list order'
    Assert-Equal -Expected 'v0.10.0' -Actual (Select-HighestStableRelease -Releases $releases).tag_name -Name 'highest ignores drafts and prereleases'
    Assert-Equal -Expected 7 -Actual (Select-HighestStableRelease -Releases @((New-Release 'v0.9.0' $false $false 7))).id -Name 'highest keeps the release object'
    Assert-True -Condition ($null -eq (Select-HighestStableRelease -Releases @())) -Name 'highest of an empty list is null'
    Assert-True -Condition ($null -eq (Select-HighestStableRelease -Releases @((New-Release 'v0.8.1' $true)))) -Name 'highest of only drafts is null'

    $stable = @((New-Release 'v0.8.7'), (New-Release 'v0.9.0'), (New-Release 'v0.9.1'))
    $ancestry = @{ 'v0.8.7' = $true; 'v0.9.0' = $true; 'v0.9.1' = $false }
    $isAncestor = { param($Tag) $ancestry[$Tag] }.GetNewClosure()
    Assert-Equal -Expected 'v0.9.0' -Actual (Select-PreviousStableRelease -Releases $stable -IsAncestor $isAncestor).tag_name -Name 'previous skips a release that is not an ancestor'
    $ancestry = @{ 'v0.8.7' = $false; 'v0.9.0' = $false; 'v0.9.1' = $false }
    $none = { param($Tag) $ancestry[$Tag] }.GetNewClosure()
    Assert-True -Condition ($null -eq (Select-PreviousStableRelease -Releases $stable -IsAncestor $none)) -Name 'previous is null when nothing is an ancestor'
    Assert-True -Condition ($null -eq (Select-PreviousStableRelease -Releases @() -IsAncestor $isAncestor)) -Name 'previous of an empty list is null'
    $ancestry = @{ 'v0.8.7' = $true; 'v0.9.0' = $false; 'v0.9.1' = $false }
    $oldest = { param($Tag) $ancestry[$Tag] }.GetNewClosure()
    Assert-Equal -Expected 'v0.8.7' -Actual (Select-PreviousStableRelease -Releases $stable -IsAncestor $oldest).tag_name -Name 'previous falls back to an older ancestor'
    $queried = New-Object System.Collections.Generic.List[string]
    $recording = { param($Tag) $queried.Add($Tag); $false }.GetNewClosure()
    Select-PreviousStableRelease -Releases @($releases) -IsAncestor $recording | Out-Null
    Assert-Equal -Expected 'v0.10.0,v0.9.1,v0.9.0,v0.8.7' -Actual ($queried -join ',') -Name 'previous only queries published stable releases, highest first'
}

function Test-ComparisonMode {
    Assert-Equal -Expected 'fresh' -Actual (Get-ComparisonMode -Kind 'stable' -EventName 'push' -ReleaseExists $false) -Name 'stable push is fresh'
    Assert-Equal -Expected 'fresh' -Actual (Get-ComparisonMode -Kind 'stable' -EventName 'push' -ReleaseExists $true) -Name 'stable push is fresh even when a release exists'
    Assert-Equal -Expected 'fresh' -Actual (Get-ComparisonMode -Kind 'stable' -EventName 'workflow_dispatch' -ReleaseExists $false) -Name 'stable dispatch without a release is fresh'
    Assert-Equal -Expected 'recovery' -Actual (Get-ComparisonMode -Kind 'stable' -EventName 'workflow_dispatch' -ReleaseExists $true) -Name 'stable dispatch with a release is recovery'
    foreach ($eventName in @('push', 'workflow_dispatch')) {
        foreach ($exists in @($false, $true)) {
            Assert-Equal -Expected 'recovery' -Actual (Get-ComparisonMode -Kind 'test' -EventName $eventName -ReleaseExists $exists) -Name "test tag on $eventName (release exists: $exists) is recovery"
        }
    }
    Assert-Throws -Action { Get-ComparisonMode -Kind 'stable' -EventName 'pull_request' -ReleaseExists $false } -MessagePattern 'Unsupported event' -Name 'unsupported event throws'
    Assert-Throws -Action { Get-ComparisonMode -Kind 'beta' -EventName 'push' -ReleaseExists $false } -MessagePattern 'Unknown tag kind' -Name 'unknown kind throws'
}
