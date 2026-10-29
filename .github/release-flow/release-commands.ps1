function Get-ReleaseCommandArguments {
    param(
        [Parameter(Mandatory)]
        [bool]$Exists,
        [Parameter(Mandatory)]
        [string]$Kind,
        [Parameter(Mandatory)]
        [string]$Tag,
        [Parameter(Mandatory)]
        [string]$NotesFile,
        [Parameter(Mandatory)]
        [bool]$IsLatest
    )

    if ($Kind -cne 'stable' -and $Kind -cne 'test') {
        throw "Unknown tag kind '$Kind'. Expected stable or test."
    }
    if ($Exists) {
        $latest = if ($IsLatest) { 'true' } else { 'false' }
        return @('edit', $Tag, '--title', $Tag, '--notes-file', $NotesFile, "--latest=$latest")
    }
    $arguments = @('create', $Tag, '--title', $Tag, '--notes-file', $NotesFile, '--verify-tag', '--latest=false')
    if ($Kind -ceq 'test') { $arguments += '--prerelease' }
    return $arguments
}

function Test-ReleaseExists {
    param(
        [Parameter(Mandatory)]
        [string]$Tag
    )

    return (Invoke-Native -FilePath 'gh' -ArgumentList @('release', 'view', $Tag, '--json', 'tagName')).ExitCode -eq 0
}
