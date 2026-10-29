function Assert-TagAccepted([string]$Tag, [string]$Kind, [string]$Version, $Sha) {
    $info = Get-ReleaseTagInfo -Tag $Tag
    Assert-True -Condition ($info.PSObject.Properties.Name -join ',' -ceq 'Tag,Kind,Version,Sha') -Name "$Tag has the documented shape"
    Assert-Equal -Expected $Tag -Actual $info.Tag -Name "$Tag tag"
    Assert-Equal -Expected $Kind -Actual $info.Kind -Name "$Tag kind"
    Assert-Equal -Expected $Version -Actual $info.Version -Name "$Tag version"
    Assert-Equal -Expected $Sha -Actual $info.Sha -Name "$Tag sha"
}

function Assert-TagRejected([string]$Tag) {
    Assert-Throws -Action { Get-ReleaseTagInfo -Tag $Tag }.GetNewClosure() -MessagePattern 'Malformed tag' -Name "reject tag '$Tag'"
}

function Test-ReleaseTagInfo {
    $sha40 = '0123456789abcdef0123456789abcdef01234567'
    Assert-TagAccepted 'v0.9.1' 'stable' '0.9.1' $null
    Assert-TagAccepted 'v10.0.0' 'stable' '10.0.0' $null
    Assert-TagAccepted 'v0.9.2-test.abc1234' 'test' '0.9.2' 'abc1234'
    Assert-TagAccepted "v1.2.3-test.$sha40" 'test' '1.2.3' $sha40
    Assert-TagAccepted 'v65534.0.0' 'stable' '65534.0.0' $null
    Assert-TagAccepted 'v0.65534.65534' 'stable' '0.65534.65534' $null
    Assert-TagAccepted 'v0.0.0' 'stable' '0.0.0' $null

    foreach ($tag in @(
            'v1.2', 'v1.2.3.4', '1.2.3', 'v01.2.3', 'v1.02.3', 'v1.2.03', 'v1.2.3-beta',
            'v1.2.3-test.', 'v1.2.3-test', 'v1.2.3-test.abc123', "v1.2.3-test.${sha40}0",
            'v1.2.3-test.ABC1234', 'v1.2.3-test.abc123g', 'v65535.0.0', 'v0.65535.0', 'v0.0.65535',
            'v0.0.99999999999999999999', 'V1.2.3', 'v1.2.3 ', ' v1.2.3', "v1.2.3`n", 'v-1.2.3', 'v1.2.3-test.abc1234-x')) {
        Assert-TagRejected $tag
    }
    Assert-Throws -Action { Get-ReleaseTagInfo -Tag 'v1.2' } -MessagePattern 'vX\.Y\.Z.*vX\.Y\.Z-test' -Name 'malformed tag message names both shapes'
}

function Test-CompareReleaseVersion {
    Assert-Equal -Expected 1 -Actual (Compare-ReleaseVersion -Left '0.10.0' -Right '0.9.9') -Name '0.10.0 > 0.9.9'
    Assert-Equal -Expected 1 -Actual (Compare-ReleaseVersion -Left '1.0.0' -Right '0.99.99') -Name '1.0.0 > 0.99.99'
    Assert-Equal -Expected 0 -Actual (Compare-ReleaseVersion -Left '0.9.1' -Right '0.9.1') -Name '0.9.1 == 0.9.1'
    Assert-Equal -Expected -1 -Actual (Compare-ReleaseVersion -Left '0.9.0' -Right '0.9.1') -Name '0.9.0 < 0.9.1'
    Assert-Equal -Expected -1 -Actual (Compare-ReleaseVersion -Left '0.9.9' -Right '0.10.0') -Name '0.9.9 < 0.10.0'
    Assert-Equal -Expected -1 -Actual (Compare-ReleaseVersion -Left '9.0.0' -Right '10.0.0') -Name '9.0.0 < 10.0.0'
    Assert-Throws -Action { Compare-ReleaseVersion -Left '1.2' -Right '1.2.3' } -MessagePattern 'Malformed version' -Name 'compare rejects short left'
    Assert-Throws -Action { Compare-ReleaseVersion -Left '1.2.3' -Right 'v1.2.3' } -MessagePattern 'Malformed version' -Name 'compare rejects v prefix'
}

function Test-NextReleaseVersion {
    Assert-Equal -Expected '0.9.2' -Actual (Get-NextReleaseVersion -Version '0.9.1' -Bump 'patch') -Name 'bump patch'
    Assert-Equal -Expected '0.10.0' -Actual (Get-NextReleaseVersion -Version '0.9.1' -Bump 'minor') -Name 'bump minor'
    Assert-Equal -Expected '1.0.0' -Actual (Get-NextReleaseVersion -Version '0.9.1' -Bump 'major') -Name 'bump major'
    Assert-Equal -Expected '1.2.4' -Actual (Get-NextReleaseVersion -Version '1.2.3' -Bump 'patch') -Name 'bump patch keeps higher components'
    Assert-Equal -Expected '1.3.0' -Actual (Get-NextReleaseVersion -Version '1.2.3' -Bump 'minor') -Name 'bump minor resets patch'
    Assert-Equal -Expected '2.0.0' -Actual (Get-NextReleaseVersion -Version '1.2.3' -Bump 'major') -Name 'bump major resets minor and patch'
    Assert-Throws -Action { Get-NextReleaseVersion -Version '0.9.1' -Bump 'huge' } -MessagePattern 'Invalid bump' -Name 'invalid bump throws'
    Assert-Throws -Action { Get-NextReleaseVersion -Version '0.9.1' -Bump 'Patch' } -MessagePattern 'Invalid bump' -Name 'bump is case-sensitive'
    Assert-Throws -Action { Get-NextReleaseVersion -Version '0.9' -Bump 'patch' } -MessagePattern 'Malformed version' -Name 'bump rejects malformed version'
    Assert-Throws -Action { Get-NextReleaseVersion -Version '0.0.65534' -Bump 'patch' } -MessagePattern 'at most 65534' -Name 'bump past the component limit throws'
}

function Test-TestReleaseTagName {
    Assert-Equal -Expected 'v0.9.2-test.abc1234' -Actual (Get-TestReleaseTagName -Version '0.9.2' -ShortSha 'abc1234') -Name 'the test tag name'
    $info = Get-ReleaseTagInfo -Tag (Get-TestReleaseTagName -Version '0.9.2' -ShortSha 'abc1234')
    Assert-Equal -Expected 'test' -Actual $info.Kind -Name 'the test tag name parses as a test tag'
    Assert-Equal -Expected '0.9.2' -Actual $info.Version -Name 'the test tag name round-trips the version'
    Assert-Equal -Expected 'abc1234' -Actual $info.Sha -Name 'the test tag name round-trips the sha'
    Assert-Equal -Expected 'v1.0.0-test.0123456789' -Actual (Get-TestReleaseTagName -Version '1.0.0' -ShortSha '0123456789') -Name 'the test tag name accepts a longer sha'
    Assert-Throws -Action { Get-TestReleaseTagName -Version '0.9.2' -ShortSha 'abc12' } -MessagePattern 'Malformed tag' -Name 'a sha shorter than seven characters is rejected'
    Assert-Throws -Action { Get-TestReleaseTagName -Version '0.9.2' -ShortSha 'ABC1234' } -MessagePattern 'Malformed tag' -Name 'an uppercase sha is rejected'
    Assert-Throws -Action { Get-TestReleaseTagName -Version '0.9' -ShortSha 'abc1234' } -MessagePattern 'Malformed tag' -Name 'a malformed version is rejected'
}
