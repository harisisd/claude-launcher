<#
.SYNOPSIS
  Builds "Claude Launcher" for Windows into win\build\.

.DESCRIPTION
  Produces a single, self-contained ClaudeLauncher.exe per architecture (no .NET install
  needed on the target PC) plus a zip ready to share:
    build\win-x64\ClaudeLauncher.exe
    build\win-arm64\ClaudeLauncher.exe
    build\ClaudeLauncher-win-x64.zip / ClaudeLauncher-win-arm64.zip

.PARAMETER Arch
  x64, arm64 or all (default: all).

.PARAMETER Install
  After building, install the build for this PC's architecture (see install.ps1).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\build.ps1
  powershell -ExecutionPolicy Bypass -File .\build.ps1 -Arch x64 -Install
#>
param(
    [ValidateSet("x64", "arm64", "all")] [string]$Arch = "all",
    [switch]$Install
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet tidak ditemukan. Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
}

# App icon from ..\assets\icon.png (only when the png is newer than the ico).
$png = Join-Path $PSScriptRoot "..\assets\icon.png"
$ico = Join-Path $PSScriptRoot "AppIcon.ico"
if ((Test-Path $png) -and (-not (Test-Path $ico) -or (Get-Item $png).LastWriteTime -gt (Get-Item $ico).LastWriteTime)) {
    Write-Host "Membuat AppIcon.ico dari assets\icon.png..."
    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Image]::FromFile((Resolve-Path $png))
    $sizes = 16, 24, 32, 48, 64, 128, 256
    $images = foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($src, 0, 0, $s, $s)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        , $ms.ToArray()
    }
    $src.Dispose()
    # ICO container with PNG-compressed entries.
    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $out
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
        $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write($img) }
    [System.IO.File]::WriteAllBytes($ico, $out.ToArray())
}

$archs = if ($Arch -eq "all") { @("x64", "arm64") } else { @($Arch) }
New-Item -ItemType Directory -Force "build" | Out-Null

foreach ($a in $archs) {
    $rid = "win-$a"
    $outDir = Join-Path $PSScriptRoot "build\$rid"
    Write-Host "`n=== Build $rid ===" -ForegroundColor Cyan
    dotnet publish ClaudeLauncher.csproj -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal untuk $rid" }

    $zip = Join-Path $PSScriptRoot "build\ClaudeLauncher-$rid.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $outDir "ClaudeLauncher.exe"), (Join-Path $PSScriptRoot "install.ps1") -DestinationPath $zip
    $size = [math]::Round((Get-Item (Join-Path $outDir "ClaudeLauncher.exe")).Length / 1MB, 1)
    Write-Host "Built: $outDir\ClaudeLauncher.exe ($size MB)" -ForegroundColor Green
    Write-Host "Zip:   $zip" -ForegroundColor Green
}

if ($Install) {
    $native = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }
    $exe = Join-Path $PSScriptRoot "build\win-$native\ClaudeLauncher.exe"
    if (-not (Test-Path $exe)) { throw "Build untuk win-$native belum ada. Jalankan build.ps1 -Arch $native." }
    & (Join-Path $PSScriptRoot "install.ps1") -Source $exe
}
