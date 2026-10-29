$script:ChangelogBlockStart = '<!-- changelog-migration:start -->'
$script:ChangelogBlockEnd = '<!-- changelog-migration:end -->'

function ConvertTo-LineFeed([AllowNull()][AllowEmptyString()][string]$Text) {
    return ("$Text").Replace("`r`n", "`n")
}

function Get-ChangelogHeadingKey([string]$Line) {
    $heading = $Line.Substring(3).Trim()
    $match = [regex]::Match($heading, '^\[([^\]]*)\](?: - .*)?$')
    if ($match.Success) { return $match.Groups[1].Value }
    return $heading
}

function ConvertFrom-Changelog {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Text
    )

    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $key = $null
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($line in @((ConvertTo-LineFeed $Text) -split "`n")) {
        if ($line.StartsWith('## ', [StringComparison]::Ordinal)) {
            if ($null -ne $key) {
                [pscustomobject]@{ Key = $key; Body = ($lines -join "`n").Trim() }
            }
            $key = Get-ChangelogHeadingKey $line
            if (-not $seen.Add($key)) {
                throw "Duplicate changelog section '$key'."
            }
            $lines = New-Object System.Collections.Generic.List[string]
            continue
        }
        $lines.Add($line)
    }
    if ($null -ne $key) {
        [pscustomobject]@{ Key = $key; Body = ($lines -join "`n").Trim() }
    }
}

function Get-ChangelogSectionTag {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Key
    )

    if ("$Key" -cmatch $script:VersionPattern) { return "v$Key" }
    return $null
}

function Get-ChangelogBlock([string]$Section) {
    return "$($script:ChangelogBlockStart)`n## Changelog`n`n$(ConvertTo-LineFeed $Section)`n$($script:ChangelogBlockEnd)`n"
}

function Set-ChangelogBlock {
    param(
        [AllowNull()][AllowEmptyString()]
        [string]$Body,
        [AllowNull()][AllowEmptyString()]
        [string]$Section
    )

    $pattern = [regex]::Escape($script:ChangelogBlockStart) + '.*?' + [regex]::Escape($script:ChangelogBlockEnd) + '\n?'
    $remaining = [regex]::Replace((ConvertTo-LineFeed $Body), $pattern, '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $block = Get-ChangelogBlock $Section
    if ([string]::IsNullOrWhiteSpace($remaining)) { return $block }
    return $remaining.TrimEnd() + "`n`n" + $block
}

function Get-ChangelogMigrationPlan {
    param(
        [AllowNull()][AllowEmptyCollection()]
        $Sections,
        [AllowNull()][AllowEmptyCollection()]
        $Releases
    )

    $byTag = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    foreach ($release in @(Select-PublishedStableRelease -Releases $Releases)) {
        $byTag[$release.tag_name] = $release
    }

    $updates = @()
    $upToDate = @()
    $empty = @()
    $unmatched = @()
    foreach ($section in @($Sections | Where-Object { $null -ne $_ })) {
        $tag = Get-ChangelogSectionTag -Key $section.Key
        if ($null -eq $tag -or -not $byTag.ContainsKey($tag)) {
            $unmatched += $section.Key
            continue
        }
        if ([string]::IsNullOrWhiteSpace($section.Body)) {
            $empty += $section.Key
            continue
        }
        $release = $byTag[$tag]
        $current = ConvertTo-LineFeed (Get-OptionalProperty $release 'body')
        $body = Set-ChangelogBlock -Body $current -Section $section.Body
        if ($body -ceq $current) {
            $upToDate += $tag
            continue
        }
        $updates += [pscustomobject]@{
            Id    = $release.id
            Tag   = $tag
            Body  = $body
            Block = Get-ChangelogBlock $section.Body
        }
    }
    return [pscustomobject]@{
        Updates   = @($updates)
        UpToDate  = @($upToDate)
        Empty     = @($empty)
        Unmatched = @($unmatched)
    }
}
