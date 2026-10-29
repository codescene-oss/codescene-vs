function Get-VersionFilePaths {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath
    )

    $directory = Join-Path $RepositoryPath 'Codescene.VSExtension.VS2022\Codescene.VSExtension.VS2022'
    return [pscustomobject]@{
        Manifest = Join-Path $directory 'source.extension.vsixmanifest'
        Source   = Join-Path $directory 'source.extension.cs'
    }
}

function Get-IdentityVersion([xml]$Document, [string]$Origin) {
    $node = $Document.SelectSingleNode("//*[local-name()='Identity']/@Version")
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.Value)) {
        throw "No Identity version found in $Origin."
    }
    return $node.Value
}

function Get-ReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath
    )

    $manifest = (Get-VersionFilePaths -RepositoryPath $RepositoryPath).Manifest
    $document = New-Object System.Xml.XmlDocument
    $document.Load($manifest)
    return Get-IdentityVersion $document $manifest
}

function Edit-VersionText {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Version
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $text = $encoding.GetString([IO.File]::ReadAllBytes($Path))
    $regex = New-Object System.Text.RegularExpressions.Regex($Pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $count = $regex.Matches($text).Count
    if ($count -ne 1) {
        throw "Version file ${Path}: expected exactly one version, found $count."
    }
    $updated = $regex.Replace($text, [System.Text.RegularExpressions.MatchEvaluator] { param($match) $match.Groups[1].Value + $Version + $match.Groups[2].Value })
    [IO.File]::WriteAllBytes($Path, $encoding.GetBytes($updated))
}

function Set-ReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Version
    )

    Get-VersionComponents $Version | Out-Null
    $paths = Get-VersionFilePaths -RepositoryPath $RepositoryPath
    Edit-VersionText -Path $paths.Manifest -Pattern '(<Identity\b[^>]*?\sVersion=")[^"]*(")' -Version $Version
    Edit-VersionText -Path $paths.Source -Pattern '(public const string Version = ")[^"]*(";)' -Version $Version
}
