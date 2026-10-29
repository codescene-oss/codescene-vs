function Get-TestGateVerdict {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Runs,
        [Parameter(Mandatory)]
        [string]$Sha
    )

    $all = @($Runs | Where-Object { $null -ne $_ })
    $guidance = "The tag must be on a tested PR merge commit, not the bot commit. Wait for or fix the 'Run tests' run for commit $Sha."

    $passed = @($all | Where-Object { $_.status -ceq 'completed' -and $_.conclusion -ceq 'success' })
    if ($passed.Count -gt 0) {
        return [pscustomobject]@{
            Pass    = $true
            Reason  = 'pass'
            Message = "Tests passed for commit $Sha in 'Run tests': $($passed[0].html_url)"
        }
    }

    $running = @($all | Where-Object { $_.status -cne 'completed' })
    if ($running.Count -gt 0) {
        return [pscustomobject]@{
            Pass    = $false
            Reason  = 'in-progress'
            Message = "The 'Run tests' run for commit $Sha is not finished: tests still running; dispatch again when done."
        }
    }

    if ($all.Count -gt 0) {
        $conclusions = ($all | ForEach-Object { "$($_.conclusion)" }) -join ', '
        return [pscustomobject]@{
            Pass    = $false
            Reason  = 'failed'
            Message = "No successful 'Run tests' run for commit $Sha. Conclusions: $conclusions. $guidance"
        }
    }

    return [pscustomobject]@{
        Pass    = $false
        Reason  = 'absent'
        Message = "No 'Run tests' run found for commit $Sha. $guidance"
    }
}

function Get-TestRun {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$Sha,
        [scriptblock]$Invoker = { param($FilePath, $ArgumentList) Invoke-Native -FilePath $FilePath -ArgumentList $ArgumentList }
    )

    $path = "repos/$Repository/actions/workflows/test.yml/runs?head_sha=$Sha&per_page=100"
    $result = & $Invoker 'gh' @('api', $path)
    if ($result.ExitCode -ne 0) {
        if ($result.Output -match 'HTTP 404') { return @() }
        throw "gh api $path failed with exit code $($result.ExitCode): $($result.Output)"
    }
    $runs = @(Get-OptionalProperty ($result.Output | ConvertFrom-Json) 'workflow_runs')
    return @($runs | Where-Object { $null -ne $_ })
}
