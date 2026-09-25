@echo off
REM ===================================================================
REM  SmartTimetable AI  -  build.bat
REM  Restores, compiles the whole solution in Release, and runs tests.
REM  Requires the .NET 10 SDK on PATH (check with: dotnet --version).
REM ===================================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo *** The .NET SDK was not found on PATH.
    echo *** Install .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo.
echo === [1/3] Restoring NuGet packages ===
dotnet restore SmartTimetable.slnx
if errorlevel 1 goto :fail

echo.
echo === [2/3] Building (Release) ===
dotnet build SmartTimetable.slnx -c Release --no-restore
if errorlevel 1 goto :fail

echo.
echo === [3/3] Running tests ===
dotnet test SmartTimetable.slnx -c Release --no-build
if errorlevel 1 goto :fail

echo.
echo ===================================================================
echo  BUILD SUCCEEDED
echo  Next: run publish.bat to produce the single-file .exe installer.
echo ===================================================================
exit /b 0

:fail
echo.
echo *** BUILD FAILED - scroll up to see the first error. ***
exit /b 1
