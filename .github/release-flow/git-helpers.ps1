function Get-TagCommit {
    param(
        [Parameter(Mandatory)]
        [string]$Tag
    )

    $result = Invoke-Native -FilePath 'git' -ArgumentList @('rev-parse', '--verify', '-q', "$Tag^{commit}")
    if ($result.ExitCode -ne 0) { return $null }
    return $result.Output.Trim()
}

function Test-GitAncestor {
    param(
        [Parameter(Mandatory)]
        [string]$Ancestor,
        [Parameter(Mandatory)]
        [string]$Descendant
    )

    $result = Invoke-Native -FilePath 'git' -ArgumentList @('merge-base', '--is-ancestor', $Ancestor, $Descendant)
    if ($result.ExitCode -eq 0) { return $true }
    if ($result.ExitCode -eq 1) { return $false }
    throw "git merge-base --is-ancestor $Ancestor $Descendant failed with exit code $($result.ExitCode): $($result.Output)"
}

function Read-FileFromHistory {
    param(
        [string]$RepositoryPath = (Get-Location).ProviderPath,
        [Parameter(Mandatory)]
        [string]$Path
    )

    $log = Invoke-NativeChecked -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'log', '-1', '--diff-filter=D', '--format=%H', '--', $Path)
    $sha = $log.Output.Trim()
    if (-not $sha) {
        throw "$Path deletion not found in git history; pass --file <path>"
    }
    return (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'show', "${sha}^:$Path")).Output
}
