function Test-GalleryRequest {
    $request = Get-GalleryRequest -Version '0.9.1'
    Assert-Equal -Expected 'Uri,Headers,Body' -Actual ($request.PSObject.Properties.Name -join ',') -Name 'the gallery request has the documented shape'
    Assert-Equal -Expected 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery' -Actual $request.Uri -Name 'the gallery request uri'
    Assert-Equal -Expected 'application/json;api-version=3.0-preview.1' -Actual $request.Headers['Accept'] -Name 'the gallery request accept header'
    Assert-Equal -Expected 'application/json' -Actual $request.Headers['Content-Type'] -Name 'the gallery request content type header'
    Assert-Equal -Expected 2 -Actual @($request.Headers.Keys).Count -Name 'the gallery request carries exactly two headers'
    $body = $request.Body | ConvertFrom-Json
    Assert-Equal -Expected 1 -Actual $body.flags -Name 'the gallery request flags value'
    Assert-Equal -Expected 1 -Actual @($body.filters).Count -Name 'the gallery request has one filter'
    Assert-Equal -Expected 1 -Actual @($body.filters[0].criteria).Count -Name 'the gallery request has one criterion'
    Assert-Equal -Expected 7 -Actual $body.filters[0].criteria[0].filterType -Name 'the gallery request filter type'
    Assert-Equal -Expected 'CodeScene.codescene-vs' -Actual $body.filters[0].criteria[0].value -Name 'the gallery request criteria value is publisher-qualified'
    Assert-Equal -Expected $request.Body -Actual (Get-GalleryRequest -Version '99.0.0').Body -Name 'the gallery request body does not depend on the version'
}

function Test-MarketplaceVersionListedSender {
    $listed = ConvertFrom-Json '{"results":[{"extensions":[{"versions":[{"version":"0.9.1"}]}]}]}'
    $sender = { param($Request) $script:Sent = $Request; return $listed }
    Assert-Equal -Expected $true -Actual (Test-MarketplaceVersionListed -Version '0.9.1' -Sender $sender) -Name 'a version present in the response is listed'
    Assert-Equal -Expected (Get-GalleryRequest -Version '0.9.1').Body -Actual $script:Sent.Body -Name 'the sender receives the gallery request body'
    Assert-Equal -Expected (Get-GalleryRequest -Version '0.9.1').Uri -Actual $script:Sent.Uri -Name 'the sender receives the gallery request uri'
    Assert-Equal -Expected $false -Actual (Test-MarketplaceVersionListed -Version '0.9.2' -Sender $sender) -Name 'a version absent from the response is not listed'
    $failing = { param($Request) throw 'connection refused' }
    Assert-Throws -Action { Test-MarketplaceVersionListed -Version '0.9.1' -Sender $failing } -MessagePattern 'Could not query the Marketplace for version 0\.9\.1.*connection refused' -Name 'a failed gallery query throws'
}

function Test-OnlyVersionFilesChanged {
    $manifest = 'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.vsixmanifest'
    $source = 'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.cs'
    Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest", " M $source")
    $script:Passed++
    Assert-OnlyVersionFilesChanged -StatusLines @(" M $source", " M $manifest")
    $script:Passed++
    Assert-OnlyVersionFilesChanged -StatusLines @("M  $manifest", "MM $source")
    $script:Passed++
    Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest`r", " M $source`r", '')
    $script:Passed++
    Assert-OnlyVersionFilesChanged -StatusLines @(" M $($manifest.Replace('/', '\'))", " M $($source.Replace('/', '\'))")
    $script:Passed++
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest", " M $source", ' M README.md') } -MessagePattern 'README\.md' -Name 'a third changed path is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest", " M $source", '?? stray.txt') } -MessagePattern 'stray\.txt' -Name 'an untracked path is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest") } -MessagePattern 'exactly the two version files' -Name 'only the manifest changed is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @(" M $source") } -MessagePattern 'exactly the two version files' -Name 'only the source changed is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @() } -MessagePattern 'no changes' -Name 'an empty status is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @('', ' ') } -MessagePattern 'no changes' -Name 'a blank status is rejected'
    Assert-Throws -Action { Assert-OnlyVersionFilesChanged -StatusLines @(" M $manifest", " M $manifest", " M $source", " M other/$source") } -MessagePattern 'other/' -Name 'a path that merely ends like a version file is rejected'
}

function New-FakePublisher([int]$ExitCode, [string]$Output) {
    $state = [pscustomobject]@{ Calls = @() }
    $block = { param($FilePath, $ArgumentList) $state.Calls += , (@($FilePath) + @($ArgumentList)); return [pscustomobject]@{ ExitCode = $ExitCode; Output = $Output } }.GetNewClosure()
    return [pscustomobject]@{ Block = $block; State = $state }
}

function New-PublishTree {
    $root = New-TempDirectory
    $manifest = Join-Path $root 'publishManifest.json'
    Write-AllText $manifest '{}'
    $vsix = New-VsixFile $root 'Codescene.VSExtension.VS2022.vsix' '0.9.2'
    return [pscustomobject]@{ Root = $root; Manifest = $manifest; Vsix = $vsix }
}

function Test-MarketplacePublishDecisions {
    $tree = New-PublishTree
    $listed = { param($Version) $true }
    $unlisted = { param($Version) $false }

    $skipped = New-FakePublisher 0 'published'
    $outcome = Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'VsixPublisher.exe' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $skipped.Block -IsListed $listed
    Assert-Equal -Expected 'listed' -Actual $outcome -Name 'a listed version is skipped'
    Assert-Equal -Expected 0 -Actual $skipped.State.Calls.Count -Name 'a listed version never calls the publisher'

    $published = New-FakePublisher 0 'Published ok'
    $outcome = Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'VsixPublisher.exe' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $published.Block -IsListed $unlisted
    Assert-Equal -Expected 'published' -Actual $outcome -Name 'exit 0 means published'
    Assert-Equal -Expected 1 -Actual $published.State.Calls.Count -Name 'the publisher is called once'
    Assert-Equal -Expected "VsixPublisher.exe|publish|-payload|$($tree.Vsix)|-publishManifest|$($tree.Manifest)" -Actual ($published.State.Calls[0] -join '|') -Name 'the publisher is called with the payload and manifest'

    $exists = New-FakePublisher 1 'Error: Extension version already exists'
    $outcome = Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'VsixPublisher.exe' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $exists.Block -IsListed $unlisted
    Assert-Equal -Expected 'exists' -Actual $outcome -Name 'exit 1 with already exists is skipped'

    $failed = New-FakePublisher 1 'Error: unauthorized'
    Assert-Throws -Action { Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'VsixPublisher.exe' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $failed.Block -IsListed $unlisted }.GetNewClosure() -MessagePattern 'exit code 1.*unauthorized' -Name 'exit 1 with other text throws the output'

    $unlistedProbe = New-FakePublisher 0 ''
    Assert-Throws -Action { Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'VsixPublisher.exe' -VsixDirectory $tree.Root -PublishManifest (Join-Path $tree.Root 'missing.json') -Publisher $unlistedProbe.Block -IsListed $unlisted }.GetNewClosure() -MessagePattern 'publishManifest\.json not found' -Name 'a missing manifest throws'
    Assert-Equal -Expected 0 -Actual $unlistedProbe.State.Calls.Count -Name 'a missing manifest never calls the publisher'

    $listCheck = { param($Version) $script:ProbedVersion = $Version; $false }
    $quiet = New-FakePublisher 0 ''
    Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'p' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $quiet.Block -IsListed $listCheck | Out-Null
    Assert-Equal -Expected '0.9.2' -Actual $script:ProbedVersion -Name 'the gallery check receives the version'

    $empty = New-TempDirectory
    Assert-Throws -Action { Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'p' -VsixDirectory $empty -PublishManifest $tree.Manifest -Publisher $unlistedProbe.Block -IsListed $unlisted }.GetNewClosure() -MessagePattern 'No \.vsix file found' -Name 'a missing vsix throws'
    New-VsixFile $tree.Root 'second.vsix' '0.9.2' | Out-Null
    Assert-Throws -Action { Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'p' -VsixDirectory $tree.Root -PublishManifest $tree.Manifest -Publisher $unlistedProbe.Block -IsListed $unlisted }.GetNewClosure() -MessagePattern 'exactly one \.vsix' -Name 'two vsix files throw'
    Assert-Equal -Expected 0 -Actual $unlistedProbe.State.Calls.Count -Name 'an invalid vsix directory never calls the publisher'

    $failingCheck = { param($Version) throw 'Could not query the Marketplace' }
    $guarded = New-FakePublisher 0 ''
    $single = New-PublishTree
    Assert-Throws -Action { Invoke-MarketplacePublish -Version '0.9.2' -PublisherPath 'p' -VsixDirectory $single.Root -PublishManifest $single.Manifest -Publisher $guarded.Block -IsListed $failingCheck }.GetNewClosure() -MessagePattern 'Could not query the Marketplace' -Name 'a failed gallery check fails the publish'
    Assert-Equal -Expected 0 -Actual $guarded.State.Calls.Count -Name 'a failed gallery check never calls the publisher'
}

function Test-MarketplaceReadIntegration {
    try {
        $listed = Test-MarketplaceVersionListed -Version '0.9.1'
    }
    catch {
        Write-Host "skipping Marketplace read integration: $($_.Exception.Message)"
        return
    }
    Assert-Equal -Expected $true -Actual $listed -Name 'the Marketplace lists version 0.9.1'
    Assert-Equal -Expected $false -Actual (Test-MarketplaceVersionListed -Version '99.0.0') -Name 'the Marketplace does not list version 99.0.0'
}
