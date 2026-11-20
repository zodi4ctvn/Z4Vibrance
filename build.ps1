#Requires -Version 5.1
<#
.SYNOPSIS
    Z4Vibrance derleme betigi (harici bagimlilik yok, sadece .NET Framework 4.x csc.exe).

.PARAMETER RegenerateIcon
    Z4Vibrance.ico dosyasini IconGenerator.cs ile yeniden uretir.

.PARAMETER Run
    Derleme basariliysa programi baslatir.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -RegenerateIcon -Run
#>
param(
    [switch]$RegenerateIcon,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

# csc.exe bul (64-bit veya 32-bit .NET Framework)
$CscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$Csc = $CscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Csc) {
    throw "csc.exe bulunamadi. .NET Framework 4.x yuklu olmali."
}

$BinDir       = Join-Path $Root "bin"
$IcoPath      = Join-Path $Root "Z4Vibrance.ico"
$ManifestPath = Join-Path $Root "app.manifest"
$OutExe       = Join-Path $BinDir "Z4Vibrance.exe"

if (-not (Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir | Out-Null
}

# Çalışan Z4Vibrance varsa uyar
$running = Get-Process -Name "Z4Vibrance" -ErrorAction SilentlyContinue
if ($running) {
    throw "Z4Vibrance su anda calisiyor. Tepsi simgesinden 'Cikis' yapip tekrar deneyin."
}

# 1. İkon (yoksa veya istenirse üret)
if ($RegenerateIcon -or -not (Test-Path $IcoPath)) {
    Write-Host "Ikon uretiliyor..."
    $GenCode = @"
using VibranceZ.Core;
class Gen { static void Main(string[] args) { IconGenerator.SaveIcoFile(args[0]); } }
"@
    $GenCs  = Join-Path $env:TEMP "vz_icogen.cs"
    $GenExe = Join-Path $env:TEMP "vz_icogen.exe"
    Set-Content -Path $GenCs -Value $GenCode -Encoding ASCII
    & $Csc /nologo /codepage:65001 /r:System.Drawing.dll /out:$GenExe $GenCs (Join-Path $Root "Core\IconGenerator.cs")
    if ($LASTEXITCODE -ne 0) { throw "Ikon uretici derlenemedi." }
    & $GenExe $IcoPath
    Remove-Item $GenCs, $GenExe -ErrorAction SilentlyContinue
}

# 2. Kaynaklar (bin / obj / .git / .github hariç)
$Excluded = @("\bin\", "\obj\", "\.git\", "\.github\", "\.vs\")
$Sources = Get-ChildItem -Path $Root -Filter "*.cs" -Recurse -File |
    Where-Object {
        $p = $_.FullName
        -not ($Excluded | Where-Object { $p -like "*$_*" })
    } |
    ForEach-Object { $_.FullName }

if (-not $Sources) { throw "Derlenecek .cs dosyasi bulunamadi." }

$ExtraArgs = @()
if (Test-Path $ManifestPath) { $ExtraArgs += "/win32manifest:$ManifestPath" }
if (Test-Path $IcoPath)      { $ExtraArgs += "/win32icon:$IcoPath" }
$PreviewImg = Join-Path $Root "assets\preview_sample.jpg"
if (Test-Path $PreviewImg)   { $ExtraArgs += "/resource:$PreviewImg,Z4Vibrance.preview_sample.jpg" }

# /codepage:65001 -> kaynak dosyalar UTF-8 (Turkce karakterler bozulmaz)
& $Csc /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /codepage:65001 `
    /out:$OutExe `
    @ExtraArgs `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Web.Extensions.dll `
    $Sources

if ($LASTEXITCODE -ne 0) {
    throw "Derleme hatasi! Cikis kodu: $LASTEXITCODE"
}

Write-Host "Derleme basarili: $OutExe" -ForegroundColor Green

if ($Run) {
    Start-Process -FilePath $OutExe
}
