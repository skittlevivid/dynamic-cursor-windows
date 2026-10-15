@echo off
chcp 65001 >nul
title отправка dynamic cursors на github

if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" (
    set "PATH=%LOCALAPPDATA%\Programs\Git\cmd;%LOCALAPPDATA%\Programs\Git\mingw64\bin;%LOCALAPPDATA%\Programs\Git\usr\bin;%PATH%"
    set "GCM_GIT_PATH=%LOCALAPPDATA%\Programs\Git\cmd\git.exe"
) else if exist "C:\Program Files\Git\cmd\git.exe" (
    set "PATH=C:\Program Files\Git\cmd;C:\Program Files\Git\mingw64\bin;C:\Program Files\Git\usr\bin;%PATH%"
    set "GCM_GIT_PATH=C:\Program Files\Git\cmd\git.exe"
)

echo ===================================================
echo     отправка dynamic cursors на github
echo ===================================================
echo.

where git >nul 2>&1
if %errorlevel% neq 0 (
    echo [ошибка] git не найден на компьютере!
    pause
    exit /b 1
)

for /f "tokens=*" %%a in ('git remote get-url origin 2^>nul') do set "EXISTING_URL=%%a"

if not "%EXISTING_URL%"=="" (
    echo текущий репозиторий: %EXISTING_URL%
    echo нажми enter чтобы отправить в него, либо вставь новую ссылку:
    set /p REPO_URL="ссылка [по умолчанию текущая]: "
    if "%REPO_URL%"=="" set "REPO_URL=%EXISTING_URL%"
) else (
    echo вставь ссылку на свой репозиторий github:
    set /p REPO_URL="ссылка: "
)

if "%REPO_URL%"=="" (
    echo.
    echo [ошибка] ссылка не может быть пустой!
    pause
    exit /b 1
)

echo.
echo [1/3] привязка репозитория...
git remote remove origin >nul 2>&1
git remote add origin %REPO_URL%
git branch -M main

echo [2/3] сохранение изменений...
git add .
git commit -m "update" >nul 2>&1

echo [3/3] отправка на github...
git push -u origin main

if %errorlevel% neq 0 (
    echo [синхронизация] отправка с флагом --force...
    git push -u origin main --force
)

if %errorlevel% equ 0 (
    echo.
    echo ===================================================
    echo     успешно загружено на github!
    echo ===================================================
) else (
    echo.
    echo [ошибка] отправка не удалась. проверь интернет или права доступа.
)

pause
