function Get-GalleryVersionEntries($Response) {
    $result = @(Get-OptionalProperty $Response 'results') | Select-Object -First 1
    $extension = @(Get-OptionalProperty $result 'extensions') | Select-Object -First 1
    return @(Get-OptionalProperty $extension 'versions')
}

function Test-GalleryHasVersion {
    param(
        [AllowNull()]
        $Response,
        [Parameter(Mandatory)]
        [string]$Version
    )

    $listed = @(Get-GalleryVersionEntries $Response | ForEach-Object { Get-OptionalProperty $_ 'version' })
    return [bool]($listed -ccontains $Version)
}

function Get-GalleryRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )

    $query = [ordered]@{
        filters = @(
            [ordered]@{
                criteria = @(
                    [ordered]@{ filterType = 7; value = 'CodeScene.codescene-vs' }
                )
            }
        )
        flags   = 1
    }
    return [pscustomobject]@{
        Uri     = 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery'
        Headers = [ordered]@{
            'Accept'       = 'application/json;api-version=3.0-preview.1'
            'Content-Type' = 'application/json'
        }
        Body    = ($query | ConvertTo-Json -Depth 6 -Compress)
    }
}

function Test-MarketplaceVersionListed {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [scriptblock]$Sender = { param($Request) Invoke-RestMethod -Method Post -Uri $Request.Uri -Headers $Request.Headers -Body $Request.Body -UseBasicParsing }
    )

    $request = Get-GalleryRequest -Version $Version
    try {
        $response = & $Sender $request
    }
    catch {
        throw "Could not query the Marketplace for version ${Version}: $($_.Exception.Message)"
    }
    return Test-GalleryHasVersion -Response $response -Version $Version
}
