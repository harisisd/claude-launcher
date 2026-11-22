<#
.SYNOPSIS
  Installs (or uninstalls) Claude Launcher for the current Windows user. No admin needed.

.DESCRIPTION
  Copies ClaudeLauncher.exe to %LOCALAPPDATA%\Programs\Claude Launcher and adds
  Start Menu + Desktop shortcuts. -Uninstall removes the program, the shortcuts and the
  "run at login" entry. Your profiles in %USERPROFILE%\.claude-launcher are kept.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
#>
param(
    [string]$Source = "",
    [switch]$NoDesktopShortcut,
    [switch]$Uninstall
)
$ErrorActionPreference = "Stop"

$dir = Join-Path $env:LOCALAPPDATA "Programs\Claude Launcher"
$exe = Join-Path $dir "ClaudeLauncher.exe"
$startMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "Claude Launcher.lnk"
$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "Claude Launcher.lnk"

# Stop a running launcher so the exe can be replaced or removed.
Get-Process ClaudeLauncher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

if ($Uninstall) {
    foreach ($p in $startMenu, $desktop) { if (Test-Path $p) { Remove-Item $p -Force } }
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ClaudeLauncher" -ErrorAction SilentlyContinue
    Write-Host "Claude Launcher sudah di-uninstall. Profil di $env:USERPROFILE\.claude-launcher tidak dihapus." -ForegroundColor Green
    return
}

if (-not $Source) {
    $candidates = @(
        (Join-Path $PSScriptRoot "ClaudeLauncher.exe"),
        (Join-Path $PSScriptRoot "build\win-x64\ClaudeLauncher.exe")
    )
    if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { $candidates = @((Join-Path $PSScriptRoot "build\win-arm64\ClaudeLauncher.exe")) + $candidates }
    $Source = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Source -or -not (Test-Path $Source)) { throw "ClaudeLauncher.exe tidak ditemukan. Taruh install.ps1 di folder yang sama dengan exe, atau pakai -Source <path>." }

New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item $Source $exe -Force
Unblock-File $exe -ErrorAction SilentlyContinue
# Keep a copy of this script next to the exe so -Uninstall works later.
if ($PSCommandPath -and ((Resolve-Path $PSCommandPath).Path -ne (Join-Path $dir "install.ps1"))) {
    Copy-Item $PSCommandPath (Join-Path $dir "install.ps1") -Force
}

$shell = New-Object -ComObject WScript.Shell
$targets = @($startMenu)
if (-not $NoDesktopShortcut) { $targets += $desktop }
foreach ($lnk in $targets) {
    $s = $shell.CreateShortcut($lnk)
    $s.TargetPath = $exe
    $s.WorkingDirectory = $dir
    $s.IconLocation = "$exe,0"
    $s.Description = "Jalankan beberapa akun Claude Desktop sekaligus"
    $s.Save()
}

# Keep the "run at login" entry pointing at the installed copy.
$run = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $run -Name "ClaudeLauncher" -ErrorAction SilentlyContinue) {
    Set-ItemProperty -Path $run -Name "ClaudeLauncher" -Value "`"$exe`" --autostart"
}

Write-Host "Terinstall: $exe" -ForegroundColor Green
Write-Host "Buka dari Start Menu atau Desktop: Claude Launcher"
