function Select-MissingAsset {
    param(
        [AllowNull()][AllowEmptyCollection()]
        [string[]]$ExistingNames,
        [AllowNull()][AllowEmptyCollection()]
        [string[]]$Paths
    )

    $attached = @($ExistingNames | Where-Object { $null -ne $_ })
    foreach ($path in @($Paths | Where-Object { $null -ne $_ })) {
        if ($attached -cnotcontains [IO.Path]::GetFileName($path)) { $path }
    }
}

$script:ReleaseAssetName = 'Codescene.VSExtension.VS2022.vsix'

function Get-AssetDownloadArguments {
    param(
        [Parameter(Mandatory)]
        [string]$Tag,
        [Parameter(Mandatory)]
        [string]$Directory
    )

    return @('release', 'download', $Tag, '--pattern', $script:ReleaseAssetName, '--dir', $Directory, '--clobber')
}

function Get-ReleaseAssetName {
    param(
        [Parameter(Mandatory)]
        [string]$Tag
    )

    $result = Invoke-NativeChecked -FilePath 'gh' -ArgumentList @('release', 'view', $Tag, '--json', 'assets')
    $assets = @(Get-OptionalProperty ($result.Output | ConvertFrom-Json) 'assets')
    return @($assets | Where-Object { $null -ne $_ } | ForEach-Object { $_.name })
}
