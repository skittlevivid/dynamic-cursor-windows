$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
    Write-Error "csc.exe not found! please ensure .net framework 4.x is installed."
    exit 1
}

$outputExe = Join-Path $PSScriptRoot "dynamic-cursors.exe"
$srcFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "src") -Filter "*.cs" | ForEach-Object { $_.FullName }

Write-Host "compiling dynamic cursors..." -ForegroundColor Cyan

$params = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/out:$outputExe"
) + $srcFiles

& $csc $params

if ($LASTEXITCODE -eq 0) {
    Write-Host "build successful: $outputExe" -ForegroundColor Green
} else {
    Write-Host "build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Compile standalone installer
$installerSource = Join-Path $PSScriptRoot "installer\Setup.cs"
if (Test-Path $installerSource) {
    $setupExe = Join-Path $PSScriptRoot "dynamic-cursors-setup.exe"
    $payloadZip = Join-Path $PSScriptRoot "payload.zip"

    Write-Host "packaging installer payload..." -ForegroundColor Cyan
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $staging = Join-Path $env:TEMP ("dc_staging_" + [Guid]::NewGuid().ToString("N"))
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging | Out-Null
    Copy-Item $outputExe $staging -Force
    Copy-Item (Join-Path $PSScriptRoot "config.ini") $staging -Force
    Copy-Item (Join-Path $PSScriptRoot "README.md") $staging -Force
    Copy-Item (Join-Path $PSScriptRoot "LICENSE") $staging -Force
    Copy-Item (Join-Path $PSScriptRoot "cursors") $staging -Recurse -Force

    if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $payloadZip)
    Remove-Item $staging -Recurse -Force

    Write-Host "compiling dynamic cursors installer..." -ForegroundColor Cyan
    $setupParams = @(
        "/nologo",
        "/target:winexe",
        "/platform:anycpu",
        "/optimize+",
        "/r:System.dll",
        "/r:System.Drawing.dll",
        "/r:System.Windows.Forms.dll",
        "/r:System.IO.Compression.dll",
        "/r:System.IO.Compression.FileSystem.dll",
        "/r:Microsoft.CSharp.dll",
        "/res:$payloadZip,payload.zip",
        "/out:$setupExe",
        $installerSource
    )

    & $csc $setupParams
    if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "installer build successful: $setupExe" -ForegroundColor Green
    } else {
        Write-Host "installer build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
