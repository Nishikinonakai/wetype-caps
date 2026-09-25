$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'WeTypeCaps'))
$localRoot = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') + '\'
if (-not $installRoot.StartsWith($localRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected install path: $installRoot"
}

$installedExe = Join-Path $installRoot 'WeTypeCaps.exe'
Get-Process -Name 'WeTypeCaps' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $installedExe } |
    Stop-Process -Force

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'WeTypeCaps' -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}
Write-Host 'Caps input method switcher uninstalled for this user.'
