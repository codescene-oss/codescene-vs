function Get-VsixManifestVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry('extension.vsixmanifest')
        if ($null -eq $entry) {
            throw "$Path does not contain extension.vsixmanifest."
        }
        $stream = $entry.Open()
        try {
            $document = New-Object System.Xml.XmlDocument
            $document.Load($stream)
        }
        finally {
            $stream.Dispose()
        }
        return Get-IdentityVersion $document "$Path extension.vsixmanifest"
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-VsixVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Version
    )

    $actual = Get-VsixManifestVersion -Path $Path
    if ($actual -cne $Version) {
        throw "VSIX version mismatch: $Path embeds version $actual but expected $Version."
    }
}
