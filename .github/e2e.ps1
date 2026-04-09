$ErrorActionPreference = "Stop"

$resultsDirectory = "Codescene.VSExtension.VS2022/TestResults/E2E"
$project = "Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022.E2ETests/Codescene.VSExtension.VS2022.E2ETests.csproj"

if (-not (Test-Path $resultsDirectory)) {
    New-Item -ItemType Directory -Path $resultsDirectory | Out-Null
}

$env:CODESCENE_E2E = "true"

dotnet test $project `
    --configuration Release `
    --no-build `
    --results-directory $resultsDirectory `
    --logger trx
