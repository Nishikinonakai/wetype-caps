param(
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$publishDirectory = Join-Path $projectDirectory 'publish'

$arguments = @(
    'publish',
    (Join-Path $projectDirectory 'WeTypeCaps.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $publishDirectory,
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

if ($SelfContained) {
    $arguments += '--self-contained'
    $arguments += 'true'
} else {
    $arguments += '--self-contained'
    $arguments += 'false'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item (Join-Path $projectDirectory 'config.example.json') (Join-Path $publishDirectory 'config.example.json') -Force
Write-Host "Published to: $publishDirectory"
