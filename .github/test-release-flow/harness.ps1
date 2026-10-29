function Format-Value($Value) {
    if ($null -eq $Value) { return '$null' }
    return "'$Value'"
}

function Fail-Assertion([string]$Name, [string]$Expected, [string]$Actual) {
    [Console]::Error.WriteLine("FAILED: $Name")
    [Console]::Error.WriteLine("  expected: $Expected")
    [Console]::Error.WriteLine("  actual:   $Actual")
    exit 1
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)][AllowNull()]$Expected,
        [Parameter(Mandatory)][AllowNull()]$Actual,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not ($Expected -ceq $Actual)) {
        Fail-Assertion $Name (Format-Value $Expected) (Format-Value $Actual)
    }
    $script:Passed++
}

function Assert-True {
    param(
        [Parameter(Mandatory)][AllowNull()]$Condition,
        [Parameter(Mandatory)][string]$Name
    )

    if ($Condition -ne $true) {
        Fail-Assertion $Name 'True' (Format-Value $Condition)
    }
    $script:Passed++
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)][scriptblock]$Action,
        [Parameter(Mandatory)][string]$MessagePattern,
        [Parameter(Mandatory)][string]$Name
    )

    try {
        & $Action | Out-Null
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            Fail-Assertion $Name "error matching '$MessagePattern'" "'$($_.Exception.Message)'"
        }
        $script:Passed++
        return
    }
    Fail-Assertion $Name "error matching '$MessagePattern'" 'no error'
}

function New-TempDirectory {
    $path = Join-Path ([IO.Path]::GetTempPath()) ('release-flow-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    $script:TempPaths += $path
    return $path
}
