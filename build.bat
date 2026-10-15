@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

echo compiling dynamic cursors...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:dynamic-cursors.exe src\*.cs

if %ERRORLEVEL% equ 0 (
    echo build successful: dynamic-cursors.exe
) else (
    echo build failed!
)
