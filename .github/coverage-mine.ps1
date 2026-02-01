$changedFiles = & "$PSScriptRoot\mine.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "0%"
    exit 0
}

$csFiles = $changedFiles -split ' ' | Where-Object { $_ -notmatch 'Tests\.cs$' }

if (-not $csFiles) {
    Write-Host "0%"
    exit 0
}

dotnet test `
    Codescene.VSExtension.VS2022/Codescene.VSExtension.sln `
    --configuration Release `
    --results-directory ./TestResults/ `
    --collect "XPlat Code Coverage" `
    --no-build `
    --verbosity quiet `
    -- `
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "0%"
    exit 1
}

$coverageFile = Get-ChildItem -Recurse -Filter "coverage.cobertura.xml" -Path "./TestResults/" | Select-Object -First 1 -ExpandProperty FullName

if (-not $coverageFile) {
    Write-Host "0%"
    exit 1
}

[xml]$coverage = Get-Content $coverageFile

$changedFilesSet = @{}
foreach ($file in $csFiles) {
    $normalizedPath = $file -replace '/', '\' -replace '^Codescene\.VSExtension\.VS2022\\', ''
    $changedFilesSet[$normalizedPath] = $true
}

$results = @()
$totalLines = 0
$coveredLines = 0

foreach ($package in $coverage.coverage.packages.package) {
    $packageName = $package.name
    foreach ($class in $package.classes.class) {
        $filename = Join-Path $packageName $class.filename

        if ($changedFilesSet.ContainsKey($filename)) {
            $classLines = 0
            $classCovered = 0

            foreach ($line in $class.lines.line) {
                $classLines++
                if ([int]$line.hits -gt 0) {
                    $classCovered++
                }
            }

            $totalLines += $classLines
            $coveredLines += $classCovered

            $percentage = if ($classLines -gt 0) { [math]::Round(($classCovered / $classLines) * 100, 1) } else { 0 }

            $results += [PSCustomObject]@{
                File = Split-Path $filename -Leaf
                Coverage = "$percentage%"
                Lines = "$classCovered/$classLines"
            }
        }
    }
}

if ($results.Count -eq 0) {
    Write-Host "0%"
    exit 1
}

$overallPercentage = if ($totalLines -gt 0) { ($coveredLines / $totalLines) * 100 } else { 0 }
$displayPercentage = [math]::Round($overallPercentage, 0)
Write-Host "$displayPercentage%"

if ($overallPercentage -ge 95.0) {
    exit 0
} else {
    exit 1
}
