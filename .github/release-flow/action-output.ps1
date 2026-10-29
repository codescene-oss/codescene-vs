function Write-ActionOutput {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [AllowNull()][AllowEmptyString()]
        [string]$Value
    )

    if (-not $env:GITHUB_OUTPUT) {
        throw 'GITHUB_OUTPUT is required to write action outputs.'
    }
    if ($Name -match '[=\r\n]' -or "$Value" -match '[\r\n]') {
        throw "Action output '$Name' must be a single line without '=' in its name."
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::AppendAllText($env:GITHUB_OUTPUT, "$Name=$Value`n", $encoding)
}
