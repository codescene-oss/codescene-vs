function Test-Utf8ConsoleEncoding {
    $skipped = $false
    try {
        [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    }
    catch [System.IO.IOException] {
        $skipped = $true
    }
    if ($skipped) {
        Write-Host 'skipping console encoding check: no console handle'
        return
    }
    Assert-Equal -Expected 65001 -Actual ([Console]::OutputEncoding.CodePage) -Name 'importing the module leaves the console output encoding at UTF-8'
}

function Test-InvokeNative {
    $failing = @('/c', 'echo oops 1>&2 & exit 3')
    $result = Invoke-Native -FilePath 'cmd.exe' -ArgumentList $failing
    Assert-Equal -Expected 3 -Actual $result.ExitCode -Name 'Invoke-Native returns the exit code'
    Assert-True -Condition ($result.Output -clike '*oops*') -Name 'Invoke-Native captures stderr in Output'
    Assert-Equal -Expected 'ExitCode,Output' -Actual ($result.PSObject.Properties.Name -join ',') -Name 'Invoke-Native result has the documented shape'

    $stderrOnly = Invoke-Native -FilePath 'cmd.exe' -ArgumentList @('/c', 'echo warn 1>&2')
    Assert-Equal -Expected 0 -Actual $stderrOnly.ExitCode -Name 'a command that only writes to stderr exits 0'
    Assert-True -Condition ($stderrOnly.Output -clike '*warn*') -Name 'stderr from a successful command is captured'

    $stdout = Invoke-Native -FilePath 'cmd.exe' -ArgumentList @('/c', 'echo hello')
    Assert-Equal -Expected 'hello' -Actual $stdout.Output -Name 'stdout is captured and trimmed'

    $mixed = Invoke-Native -FilePath 'cmd.exe' -ArgumentList @('/c', 'echo one& echo two 1>&2& echo three')
    Assert-Equal -Expected 3 -Actual @($mixed.Output -split "`n").Count -Name 'output lines from both streams are kept'

    $none = Invoke-Native -FilePath 'cmd.exe' -ArgumentList @('/c', 'exit 0')
    Assert-Equal -Expected '' -Actual $none.Output -Name 'a silent command has empty output'
    Assert-Equal -Expected 0 -Actual (Invoke-Native -FilePath 'whoami.exe' -ArgumentList $null).ExitCode -Name 'a null argument list is accepted'

    Assert-Throws -Action { Invoke-NativeChecked -FilePath 'cmd.exe' -ArgumentList $failing } -MessagePattern 'oops' -Name 'Invoke-NativeChecked includes the output'
    Assert-Throws -Action { Invoke-NativeChecked -FilePath 'cmd.exe' -ArgumentList $failing } -MessagePattern 'exit code 3' -Name 'Invoke-NativeChecked includes the exit code'
    Assert-Equal -Expected 0 -Actual (Invoke-NativeChecked -FilePath 'cmd.exe' -ArgumentList @('/c', 'echo warn 1>&2')).ExitCode -Name 'Invoke-NativeChecked passes a stderr-only success'
}

function Test-WriteActionOutput {
    $directory = New-TempDirectory
    $file = Join-Path $directory 'output.txt'
    $saved = $env:GITHUB_OUTPUT
    try {
        $env:GITHUB_OUTPUT = $file
        Write-ActionOutput -Name 'tag' -Value 'v0.9.1'
        Write-ActionOutput -Name 'notes_start' -Value ''
        Write-ActionOutput -Name 'text' -Value ([string][char]0x00E5 + 'ngstr' + [char]0x00F6 + 'm')
        $bytes = [IO.File]::ReadAllBytes($file)
        Assert-True -Condition ($bytes[0] -ne 0xEF) -Name 'the output file starts without a BOM'
        $lines = @((New-Object System.Text.UTF8Encoding($false)).GetString($bytes) -split "`n" | Where-Object { $_ })
        Assert-Equal -Expected 'tag=v0.9.1' -Actual $lines[0] -Name 'the first output line is name=value'
        Assert-Equal -Expected 'notes_start=' -Actual $lines[1] -Name 'an empty value is written as name='
        Assert-Equal -Expected ('text=' + [char]0x00E5 + 'ngstr' + [char]0x00F6 + 'm') -Actual $lines[2] -Name 'non-ASCII values are written as UTF-8'
        Assert-Equal -Expected 3 -Actual $lines.Count -Name 'outputs are appended one per line'
        Assert-Equal -Expected 0 -Actual (Get-CarriageReturnCount $bytes) -Name 'output lines end with LF only'

        Assert-Throws -Action { Write-ActionOutput -Name 'a' -Value "x`ny=1" } -MessagePattern 'single line' -Name 'a multi-line value is rejected'
        Assert-Throws -Action { Write-ActionOutput -Name 'a=b' -Value 'x' } -MessagePattern 'single line' -Name 'a name containing = is rejected'
        Assert-Equal -Expected 3 -Actual @((New-Object System.Text.UTF8Encoding($false)).GetString([IO.File]::ReadAllBytes($file)) -split "`n" | Where-Object { $_ }).Count -Name 'rejected outputs write nothing'

        $env:GITHUB_OUTPUT = $null
        Assert-Throws -Action { Write-ActionOutput -Name 'a' -Value 'x' } -MessagePattern 'GITHUB_OUTPUT is required' -Name 'writing without GITHUB_OUTPUT throws'
    }
    finally {
        $env:GITHUB_OUTPUT = $saved
    }
}

function Test-GitHelpers {
    $head = (Invoke-NativeChecked -FilePath 'git' -ArgumentList @('rev-parse', 'HEAD')).Output
    Assert-Equal -Expected $head -Actual (Get-TagCommit -Tag 'HEAD') -Name 'Get-TagCommit resolves a ref to the full commit'
    Assert-True -Condition ($null -eq (Get-TagCommit -Tag 'v0.0.0-no-such-tag')) -Name 'Get-TagCommit returns null for an unknown tag'

    $parent = Invoke-Native -FilePath 'git' -ArgumentList @('rev-parse', '--verify', '-q', 'HEAD~1')
    if ($parent.ExitCode -ne 0) {
        Write-Host 'skipping ancestry checks: no HEAD~1 in this checkout'
        return
    }
    Assert-Equal -Expected $true -Actual (Test-GitAncestor -Ancestor 'HEAD~1' -Descendant 'HEAD') -Name 'the parent is an ancestor of HEAD'
    Assert-Equal -Expected $false -Actual (Test-GitAncestor -Ancestor 'HEAD' -Descendant 'HEAD~1') -Name 'HEAD is not an ancestor of its parent'
    Assert-Equal -Expected $true -Actual (Test-GitAncestor -Ancestor 'HEAD' -Descendant 'HEAD') -Name 'a commit is an ancestor of itself'
    Assert-Throws -Action { Test-GitAncestor -Ancestor 'no-such-ref-anywhere' -Descendant 'HEAD' } -MessagePattern 'exit code 128' -Name 'an unknown ref throws instead of answering false'
}
