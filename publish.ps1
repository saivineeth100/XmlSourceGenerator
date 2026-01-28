param (
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [switch]$PackOnly,
    [switch]$SkipTest
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "XmlSourceGenerator.sln"
$projectPath = Join-Path $PSScriptRoot "src\XmlSourceGenerator\XmlSourceGenerator.csproj"
$artifactsPath = Join-Path $PSScriptRoot "artifacts"

Write-Host "Cleaning artifacts..." -ForegroundColor Cyan
if (Test-Path $artifactsPath) {
    Remove-Item $artifactsPath -Recurse -Force
}
New-Item -ItemType Directory -Path $artifactsPath | Out-Null

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore $solutionPath

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build $solutionPath --configuration Release --no-restore

if (-not $SkipTest) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test $solutionPath --configuration Release --no-build --verbosity normal
}

Write-Host "Packing NuGet package..." -ForegroundColor Cyan
dotnet pack $projectPath --configuration Release --no-build --output $artifactsPath

if ($PackOnly) {
    Write-Host "Pack only mode enabled. Nuget package created at $artifactsPath" -ForegroundColor Green
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host "Publishing to NuGet..." -ForegroundColor Cyan
    $packageFile = Get-ChildItem $artifactsPath -Filter "*.nupkg" | Select-Object -First 1
    if ($packageFile) {
        dotnet nuget push $packageFile.FullName --api-key $ApiKey --source $Source --skip-duplicate
        Write-Host "Successfully published $($packageFile.Name)" -ForegroundColor Green
    } else {
        Write-Error "No NuGet package found in $artifactsPath"
    }
} else {
    Write-Host "No ApiKey provided. Skipping publish." -ForegroundColor Yellow
    Write-Host "To publish, provide an ApiKey: .\publish.ps1 -ApiKey <key>" -ForegroundColor Gray
}
