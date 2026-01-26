$tracked = git ls-files -s '*.cs' '*.csproj' '.codescene/*.json' '*.csproj' | ForEach-Object {
    $p = $_ -split '\s+', 4
    [PSCustomObject]@{Hash = $p[1]; Path = $p[3]}
}

$untracked = git status --untracked-files=all --short --porcelain |
    ForEach-Object { $_.Substring(3).Trim() } |
    Where-Object { ($_ -like '*.cs' -or $_ -like '*.csproj' -or $_ -like '.codescene/*.json') -and (Test-Path $_) } |
    ForEach-Object {
        [PSCustomObject]@{
            Hash = (git hash-object $_).Trim()
            Path = $_
        }
    }

$allFiles = $tracked + $untracked | Sort-Object Path
$concat = ($allFiles.Hash -join '')

$bytes = [Text.Encoding]::UTF8.GetBytes($concat)
$stream = [IO.MemoryStream]::new($bytes)
$cacheSha = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash.Substring(0,12)
$stream.Dispose()

Write-Output $cacheSha
