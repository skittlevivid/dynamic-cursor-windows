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
