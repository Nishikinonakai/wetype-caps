$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\WeTypeCaps.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Cannot find $exe. Run build.ps1 first."
}
Start-Process -FilePath $exe -WindowStyle Hidden
Write-Host 'Choose Yes in the installation dialog.'
