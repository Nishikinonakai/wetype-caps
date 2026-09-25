$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$publishDirectory = Join-Path $projectDirectory 'dist'

$arguments = @(
    'publish',
    (Join-Path $projectDirectory 'WeTypeCaps.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $publishDirectory,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Double-click to install: $(Join-Path $publishDirectory 'WeTypeCaps.exe')"
