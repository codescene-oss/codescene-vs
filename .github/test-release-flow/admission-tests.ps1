function Assert-DispatchPasses([string]$EventName, [string]$RunRef, [bool]$TagExists, [bool]$DryRun, [string]$Kind, [string]$Name) {
    Assert-DispatchAllowed -EventName $EventName -RunRef $RunRef -TagExists $TagExists -DryRun $DryRun -Kind $Kind
    $script:Passed++
}

function Test-DispatchAllowed {
    $main = 'refs/heads/main'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef 'refs/heads/other' -TagExists $true -DryRun $false -Kind 'stable' } -MessagePattern 'must be dispatched from main' -Name 'dispatch from another branch throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef 'refs/tags/v0.9.1' -TagExists $true -DryRun $false -Kind 'stable' } -MessagePattern 'must be dispatched from main' -Name 'dispatch from a tag ref throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef '' -TagExists $true -DryRun $false -Kind 'stable' } -MessagePattern 'must be dispatched from main' -Name 'dispatch with an empty ref throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef 'refs/heads/main2' -TagExists $true -DryRun $false -Kind 'stable' } -MessagePattern 'must be dispatched from main' -Name 'dispatch from a lookalike branch throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef $main -TagExists $false -DryRun $false -Kind 'stable' } -MessagePattern 'does not exist' -Name 'dispatch with a missing tag throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef $main -TagExists $true -DryRun $true -Kind 'test' } -MessagePattern 'Dry run needs a stable tag' -Name 'dry run on a test tag throws'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef 'refs/heads/other' -TagExists $false -DryRun $true -Kind 'test' } -MessagePattern 'must be dispatched from main' -Name 'the ref is checked before the tag and dry run'
    Assert-Throws -Action { Assert-DispatchAllowed -EventName 'workflow_dispatch' -RunRef $main -TagExists $false -DryRun $true -Kind 'test' } -MessagePattern 'does not exist' -Name 'the tag is checked before the dry run'
    Assert-DispatchPasses 'workflow_dispatch' $main $true $true 'stable' 'dry run on a stable tag passes'
    Assert-DispatchPasses 'workflow_dispatch' $main $true $false 'stable' 'real stable dispatch passes'
    Assert-DispatchPasses 'workflow_dispatch' $main $true $false 'test' 'real test dispatch passes'
    Assert-DispatchPasses 'push' 'refs/tags/v0.9.2' $false $true 'test' 'push event skips every dispatch check'
}

function Get-AdmissionFacts([hashtable]$Facts) {
    $values = @{ OnMain = $true; Highest = $null; HighestIsAncestor = $null; Previous = $null; TaggedCommit = $null }
    foreach ($key in $Facts.Keys) { $values[$key] = $Facts[$key] }
    return New-TagAdmissionFacts @values
}

function Assert-AdmissiblePasses($TagInfo, [string]$Mode, [hashtable]$Facts, [string]$Name) {
    Assert-TagAdmissible -TagInfo $TagInfo -Mode $Mode -Facts (Get-AdmissionFacts $Facts)
    $script:Passed++
}

function Assert-AdmissibleThrows($TagInfo, [string]$Mode, [hashtable]$Facts, [string]$Pattern, [string]$Name) {
    $admission = Get-AdmissionFacts $Facts
    Assert-Throws -Action { Assert-TagAdmissible -TagInfo $TagInfo -Mode $Mode -Facts $admission }.GetNewClosure() -MessagePattern $Pattern -Name $Name
}

function Test-AdmissionFacts {
    $facts = New-TagAdmissionFacts -OnMain $true -Highest (New-Release 'v0.9.1') -HighestIsAncestor $true -Previous $null -TaggedCommit 'abc'
    Assert-Equal -Expected 'OnMain,Highest,HighestIsAncestor,Previous,TaggedCommit' -Actual ($facts.PSObject.Properties.Name -join ',') -Name 'admission facts have the documented shape'
    Assert-True -Condition ($facts.OnMain -eq $true) -Name 'admission facts keep OnMain'
    Assert-Equal -Expected 'v0.9.1' -Actual $facts.Highest.tag_name -Name 'admission facts keep the highest release'
    Assert-True -Condition ($null -eq $facts.Previous) -Name 'admission facts keep a null previous release'
    Assert-Equal -Expected 'abc' -Actual $facts.TaggedCommit -Name 'admission facts keep the tagged commit'
    $bare = New-TagAdmissionFacts -OnMain $false -Highest $null -HighestIsAncestor $null -Previous $null -TaggedCommit $null
    Assert-True -Condition ($bare.OnMain -eq $false -and $null -eq $bare.Highest -and $null -eq $bare.HighestIsAncestor -and $null -eq $bare.Previous) -Name 'admission facts accept every release argument as null'
}

function Test-FreshAdmission {
    $stable = Get-ReleaseTagInfo -Tag 'v0.9.2'
    $v091 = Get-ReleaseTagInfo -Tag 'v0.9.1'
    $v090 = Get-ReleaseTagInfo -Tag 'v0.9.0'
    $v093 = Get-ReleaseTagInfo -Tag 'v0.9.3'
    $exceed = 'does not exceed the release already shipped'

    Assert-AdmissiblePasses $stable 'fresh' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $true; Previous = (New-Release 'v0.9.1'); TaggedCommit = '015c6a0ffffffffffffffffffffffffffffffff0' } 'fresh above an ancestor highest passes'
    Assert-AdmissiblePasses $stable 'fresh' @{} 'fresh without any release passes'
    Assert-AdmissiblePasses $stable 'fresh' @{ Highest = (New-Release 'v0.9.1') } 'fresh with an unknown ancestry passes'
    Assert-AdmissiblePasses $stable 'fresh' @{ Highest = 'v0.9.1'; HighestIsAncestor = $true } 'fresh accepts a highest given as a tag name'
    Assert-AdmissiblePasses (Get-ReleaseTagInfo -Tag 'v0.10.0') 'fresh' @{ Highest = (New-Release 'v0.9.9'); HighestIsAncestor = $true } 'fresh compares versions numerically'

    Assert-AdmissibleThrows $v091 'fresh' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $true } $exceed 'fresh equal to highest throws'
    Assert-AdmissibleThrows $v090 'fresh' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $true } $exceed 'fresh below highest throws'
    Assert-AdmissibleThrows $v093 'fresh' @{ Highest = (New-Release 'v0.9.2'); HighestIsAncestor = $false } 'predates the newest release' 'fresh whose highest release is not an ancestor throws'
    Assert-AdmissibleThrows $v091 'fresh' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $false } 'does not exceed' 'the version check precedes the ancestry check'
    Assert-AdmissibleThrows $v093 'fresh' @{ OnMain = $false; Highest = (New-Release 'v0.9.2'); HighestIsAncestor = $false } 'not on main' 'the main check precedes the ancestry check'
}

function Test-RecoveryAdmission {
    $v090 = Get-ReleaseTagInfo -Tag 'v0.9.0'
    $exceed = 'does not exceed the release already shipped'

    Assert-AdmissiblePasses $v090 'recovery' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $false; Previous = (New-Release 'v0.8.7') } 'recovery passes even though a higher release exists'
    Assert-AdmissiblePasses $v090 'recovery' @{} 'recovery without any release passes'
    Assert-AdmissiblePasses $v090 'recovery' @{ Previous = 'v0.8.7' } 'recovery accepts a previous given as a tag name'
    Assert-AdmissibleThrows $v090 'recovery' @{ Previous = (New-Release 'v0.9.0') } $exceed 'recovery equal to previous throws'
    Assert-AdmissibleThrows $v090 'recovery' @{ Previous = (New-Release 'v0.9.1') } $exceed 'recovery below previous throws'
    Assert-AdmissibleThrows $v090 'recovery' @{ Previous = (New-Release 'v0.9.1') } 'shipped before it \(v0\.9\.1\)' 'the recovery message names the previous release'
    Assert-AdmissibleThrows $v090 'recovery' @{ Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $true; Previous = (New-Release 'v0.9.0') } '\(v0\.9\.0\)' 'recovery compares against previous, not highest'
}

function Test-TestTagAdmission {
    $sha = '015c6a0'
    $fullSha = '015c6a0ffffffffffffffffffffffffffffffff0'
    $test = Get-ReleaseTagInfo -Tag "v0.9.1-test.$sha"

    Assert-AdmissibleThrows $test 'recovery' @{ OnMain = $false; Highest = (New-Release 'v0.9.1'); HighestIsAncestor = $true; Previous = (New-Release 'v0.9.1'); TaggedCommit = $fullSha } 'does not exceed' 'test base equal to previous throws'
    Assert-AdmissiblePasses $test 'recovery' @{ OnMain = $false; TaggedCommit = $fullSha } 'a test tag with no previous passes off main'
    Assert-AdmissiblePasses $test 'recovery' @{ OnMain = $false; Previous = (New-Release 'v0.9.0'); TaggedCommit = $fullSha } 'a test tag above previous passes'
    Assert-AdmissiblePasses $test 'recovery' @{ OnMain = $false; Previous = 'v0.9.0'; TaggedCommit = $sha } 'a test sha equal to the tagged commit passes'
    Assert-AdmissibleThrows $test 'recovery' @{ TaggedCommit = 'ffffffffffffffffffffffffffffffffffffffff' } 'Sha mismatch' 'test sha mismatch throws'
    Assert-AdmissibleThrows $test 'recovery' @{ TaggedCommit = 'ab015c6a0ffffffffffffffffffffffffffffff0' } 'Sha mismatch' 'test sha must be a prefix, not a substring'
    Assert-AdmissibleThrows $test 'recovery' @{ TaggedCommit = $null } 'Sha mismatch' 'test tag without a tagged commit fails closed'
    Assert-AdmissibleThrows $test 'recovery' @{ TaggedCommit = '' } 'Sha mismatch' 'test tag with an empty tagged commit fails closed'
    Assert-AdmissibleThrows $test 'recovery' @{ Previous = (New-Release 'v0.9.1'); TaggedCommit = 'ffffffffffffffffffffffffffffffffffffffff' } 'does not exceed' 'the version check precedes the sha check'
}

function Test-TagAdmissible {
    $stable = Get-ReleaseTagInfo -Tag 'v0.9.2'
    $v090 = Get-ReleaseTagInfo -Tag 'v0.9.0'
    $test = Get-ReleaseTagInfo -Tag 'v0.9.1-test.015c6a0'

    Test-AdmissionFacts
    Test-FreshAdmission
    Test-RecoveryAdmission
    Test-TestTagAdmission
    Assert-AdmissibleThrows $stable 'fresh' @{ OnMain = $false } 'not on main' 'stable tag not on main throws'
    Assert-AdmissibleThrows $v090 'recovery' @{ OnMain = $false } 'not on main' 'recovery of a stable tag not on main throws'
    Assert-AdmissiblePasses $stable 'fresh' @{ OnMain = $true } 'a stable tag on main passes'
    Assert-AdmissibleThrows $test 'bogus' @{ TaggedCommit = '015c6a0ffffffffffffffffffffffffffffffff0' } 'bogus' 'unknown mode is rejected'
}
