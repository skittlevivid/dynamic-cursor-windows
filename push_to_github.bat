@echo off
chcp 65001 >nul
title загрузка dynamic cursors на github
echo ===================================================
echo     загрузка dynamic cursors на github
echo ===================================================
echo.
echo 1. открой браузер и перейди на: https://github.com/new
echo 2. укажи имя репозитория (например: dynamic-cursors)
echo 3. НЕ ставь галочки на README, .gitignore и License
echo 4. нажми зеленую кнопку "Create repository"
echo 5. скопируй ссылку (https://github.com/.../....git)
echo.
echo ===================================================
set /p REPO_URL="вставь ссылку на свой репозиторий и нажми enter: "

if "%REPO_URL%"=="" (
    echo.
    echo [ошибка] ссылка не может быть пустой!
    pause
    exit /b 1
)

echo.
echo [1/4] проверка git...
where git >nul 2>&1
if %errorlevel% neq 0 (
    if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" (
        set "PATH=%LOCALAPPDATA%\Programs\Git\cmd;%PATH%"
    ) else if exist "C:\Program Files\Git\cmd\git.exe" (
        set "PATH=C:\Program Files\Git\cmd;%PATH%"
    )
)

where git >nul 2>&1
if %errorlevel% neq 0 (
    echo [ошибка] git не найден на компьютере!
    pause
    exit /b 1
)

echo [2/4] привязка репозитория...
git remote remove origin >nul 2>&1
git remote add origin %REPO_URL%

echo [3/4] переключение ветки на main...
git branch -M main

echo [4/4] отправка файлов на github...
git push -u origin main

if %errorlevel% equ 0 (
    echo.
    echo ===================================================
    echo     успешно загружено на github!
    echo ===================================================
) else (
    echo.
    echo [внимание] если вылезло окно входа github - авторизуйся через браузер.
)

pause
