function Get-ComparisonMode {
    param(
        [Parameter(Mandatory)]
        [string]$Kind,
        [Parameter(Mandatory)]
        [string]$EventName,
        [Parameter(Mandatory)]
        [bool]$ReleaseExists
    )

    if ($Kind -cne 'stable' -and $Kind -cne 'test') {
        throw "Unknown tag kind '$Kind'. Expected stable or test."
    }
    if ($EventName -cne 'push' -and $EventName -cne 'workflow_dispatch') {
        throw "Unsupported event '$EventName'. Expected push or workflow_dispatch."
    }
    if ($Kind -ceq 'test') { return 'recovery' }
    if ($EventName -ceq 'push') { return 'fresh' }
    if ($ReleaseExists) { return 'recovery' }
    return 'fresh'
}

function Assert-DispatchAllowed {
    param(
        [Parameter(Mandatory)]
        [string]$EventName,
        [AllowNull()][AllowEmptyString()]
        [string]$RunRef,
        [Parameter(Mandatory)]
        [bool]$TagExists,
        [Parameter(Mandatory)]
        [bool]$DryRun,
        [Parameter(Mandatory)]
        [string]$Kind
    )

    if ($EventName -cne 'workflow_dispatch') { return }
    if ($RunRef -cne 'refs/heads/main') {
        throw "The release workflow must be dispatched from main, but was dispatched from '$RunRef'."
    }
    if (-not $TagExists) {
        throw 'The tag does not exist. Create and push the tag before dispatching the workflow.'
    }
    if ($DryRun -and $Kind -cne 'stable') {
        throw 'Dry run needs a stable tag. Test tags always publish a prerelease.'
    }
}
