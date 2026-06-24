@echo off
setlocal

echo ============================================
echo   Glint Build Script (Northbyte Studios)
echo ============================================

set CONFIG=Release
set RUNTIME=win-x64
set PROJECT=src\Glint.App\Glint.App.csproj
set OUTDIR=dist\Glint
set TEMPDIR=publish_temp
set LOGFILE=build_log.txt

echo.
echo [1/3] Cleaning previous build output...
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
if exist "%TEMPDIR%" rmdir /s /q "%TEMPDIR%"
if exist "%LOGFILE%" del /q "%LOGFILE%"

echo.
echo [2/3] Publishing (self-contained, single-file, %RUNTIME%)...
echo       Full output is being saved to %LOGFILE% as well as shown below.
echo.

dotnet publish "%PROJECT%" -c %CONFIG% -r %RUNTIME% --self-contained true -p:PublishSingleFile=true -o "%TEMPDIR%" > "%LOGFILE%" 2>&1
set BUILD_RESULT=%ERRORLEVEL%

type "%LOGFILE%"

if not %BUILD_RESULT%==0 (
    echo.
    echo ============================================
    echo   BUILD FAILED - see errors above
    echo   Full output saved to: %LOGFILE%
    echo ============================================
    echo.
    echo Press any key to close this window...
    pause >nul
    exit /b 1
)

echo.
echo [3/3] Assembling distributable folder: %OUTDIR%
mkdir "%OUTDIR%"
copy /y "%TEMPDIR%\Glint.exe" "%OUTDIR%\" >nul
copy /y "%TEMPDIR%\Glint.pdb" "%OUTDIR%\" >nul 2>nul
copy /y "LICENSE.txt" "%OUTDIR%\" >nul
copy /y "README.md" "%OUTDIR%\" >nul
copy /y "THIRD_PARTY_NOTICES.txt" "%OUTDIR%\" >nul
copy /y "CHANGELOG.md" "%OUTDIR%\" >nul

rmdir /s /q "%TEMPDIR%"

echo.
echo ============================================
echo   BUILD COMPLETE
echo   Output: %OUTDIR%\Glint.exe
echo ============================================
echo.
echo Press any key to close this window...
pause >nul

endlocal
