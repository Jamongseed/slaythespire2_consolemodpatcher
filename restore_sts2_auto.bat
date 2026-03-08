@echo off
setlocal
chcp 65001 >nul
title StS2 DLL Restore

set "PATCHER_PATH=%~dp0StS2DllPatcher.exe"

if not exist "%PATCHER_PATH%" (
    echo [ERROR] 같은 폴더에 StS2DllPatcher.exe가 없습니다.
    echo %PATCHER_PATH%
    echo.
    pause
    exit /b 1
)

echo ========================================
echo  StS2 DLL RESTORE
echo ========================================
echo.
echo Steam 라이브러리에서 sts2.dll 자동 탐색 후 복원을 시도합니다.
echo.

"%PATCHER_PATH%" --restore
set "EXITCODE=%ERRORLEVEL%"

echo.
if "%EXITCODE%"=="0" (
    echo [OK] 복원이 완료되었습니다.
) else (
    echo [FAIL] 복원에 실패했습니다. 종료 코드: %EXITCODE%
)

echo.
pause
exit /b %EXITCODE%