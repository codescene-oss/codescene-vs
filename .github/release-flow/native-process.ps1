function Set-Utf8ConsoleEncoding {
    try {
        [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    }
    catch [System.IO.IOException] {
    }
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [AllowNull()][AllowEmptyCollection()]
        [string[]]$ArgumentList
    )

    $arguments = @($ArgumentList | Where-Object { $null -ne $_ })
    $ErrorActionPreference = 'Continue'
    $lines = & $FilePath @arguments 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output   = (@($lines) -join "`n").TrimEnd()
    }
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [AllowNull()][AllowEmptyCollection()]
        [string[]]$ArgumentList
    )

    $result = Invoke-Native -FilePath $FilePath -ArgumentList $ArgumentList
    if ($result.ExitCode -ne 0) {
        throw "$FilePath $(@($ArgumentList) -join ' ') failed with exit code $($result.ExitCode): $($result.Output)"
    }
    return $result
}
