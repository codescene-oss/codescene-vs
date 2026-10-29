function Test-VersionRecordNeeded {
    param(
        [Parameter(Mandatory)]
        [string]$MainVersion,
        [Parameter(Mandatory)]
        [string]$Version
    )

    return (Compare-ReleaseVersion -Left $Version -Right $MainVersion) -gt 0
}

function Assert-OnlyVersionFilesChanged {
    param(
        [AllowNull()][AllowEmptyCollection()][AllowEmptyString()]
        [string[]]$StatusLines
    )

    $expected = @(
        'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.cs',
        'Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022/source.extension.vsixmanifest'
    )
    $lines = @($StatusLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $changed = @($lines | ForEach-Object { $_.TrimEnd("`r").Substring(3).Replace('\', '/') } | Sort-Object -Unique)
    if (($changed -join '|') -cne ($expected -join '|')) {
        $reported = if ($changed.Count -eq 0) { 'no changes' } else { $changed -join ', ' }
        throw "Expected exactly the two version files to change but git status reports: $reported."
    }
}

function Test-PushRejected {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Output
    )

    return [bool]("$Output" -match 'rejected|non-fast-forward|fetch first')
}

function Invoke-VersionRecord {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,
        [Parameter(Mandatory)]
        [string]$Version,
        [ValidateRange(1, 100)]
        [int]$Attempts = 5
    )

    Get-VersionComponents $Version | Out-Null
    $identity = @('-c', 'user.name=github-actions[bot]', '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com')
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        Invoke-NativeChecked -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'fetch', 'origin', 'main') | Out-Null
        Invoke-NativeChecked -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'checkout', '-B', 'main', 'origin/main') | Out-Null

        $mainVersion = Get-ReleaseVersion -RepositoryPath $RepositoryPath
        if (-not (Test-VersionRecordNeeded -MainVersion $mainVersion -Version $Version)) {
            Write-Host "main already at $mainVersion"
            return [pscustomobject]@{ Changed = $false }
        }

        Set-ReleaseVersion -RepositoryPath $RepositoryPath -Version $Version
        $status = Invoke-NativeChecked -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'status', '--porcelain', '--untracked-files=all')
        Assert-OnlyVersionFilesChanged -StatusLines @($status.Output -split "`n")

        Invoke-NativeChecked -FilePath 'git' -ArgumentList (@('-C', $RepositoryPath) + $identity + @('commit', '-am', "chore(release): set version to $Version")) | Out-Null
        $push = Invoke-Native -FilePath 'git' -ArgumentList @('-C', $RepositoryPath, 'push', 'origin', 'HEAD:refs/heads/main')
        if ($push.ExitCode -eq 0) {
            Write-Host "Recorded version $Version on main (attempt $attempt)"
            return [pscustomobject]@{ Changed = $true; Attempt = $attempt }
        }
        if (-not (Test-PushRejected -Output $push.Output)) {
            throw "git push origin HEAD:refs/heads/main failed with exit code $($push.ExitCode): $($push.Output)"
        }
        Write-Host "Push of version $Version was rejected on attempt $attempt of ${Attempts}: $($push.Output)"
    }
    throw "Could not record version $Version on main after $Attempts attempts"
}
