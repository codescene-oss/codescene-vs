function Get-GenerateNotesArguments {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$Tag,
        [AllowNull()][AllowEmptyString()]
        [string]$PreviousTag
    )

    $arguments = @('api', '-X', 'POST', "repos/$Repository/releases/generate-notes", '-f', "tag_name=$Tag")
    if (-not [string]::IsNullOrEmpty($PreviousTag)) {
        $arguments += @('-f', "previous_tag_name=$PreviousTag")
    }
    return $arguments
}

function Get-GeneratedReleaseNotes {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$Tag,
        [AllowNull()][AllowEmptyString()]
        [string]$PreviousTag
    )

    $arguments = Get-GenerateNotesArguments -Repository $Repository -Tag $Tag -PreviousTag $PreviousTag
    $result = Invoke-NativeChecked -FilePath 'gh' -ArgumentList $arguments
    $body = Get-OptionalProperty ($result.Output | ConvertFrom-Json) 'body'
    if ($null -eq $body) {
        throw "GitHub returned no release notes body for $Tag."
    }
    return [string]$body
}
