$ErrorActionPreference = 'Stop'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'WeTypeCaps' -ErrorAction SilentlyContinue

Get-Process -Name 'WeTypeCaps' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq (Join-Path $PSScriptRoot 'publish\WeTypeCaps.exe') } |
    Stop-Process

Write-Host 'Startup entry removed and WeType Caps stopped. Program files were kept.'
