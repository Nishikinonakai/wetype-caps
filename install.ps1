param(
    [switch]$NoStartup
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'publish\WeTypeCaps.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Cannot find $exe. Run build.ps1 first."
}

if (-not $NoStartup) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty -Path $runKey -Name 'WeTypeCaps' -Value "`"$exe`"" -PropertyType String -Force | Out-Null
}

$running = Get-Process -Name 'WeTypeCaps' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe }
if (-not $running) {
    Start-Process -FilePath $exe -WindowStyle Hidden
}
Write-Host 'WeType Caps started. Use its tray menu to pause, inspect, or exit.'
