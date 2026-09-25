@echo off
REM ===================================================================
REM  SmartTimetable AI  -  publish.bat
REM  Produces ONE self-contained SmartTimetable.exe for Windows x64.
REM  No .NET runtime needs to be installed on the college's PC.
REM
REM  Notes:
REM   * Self-contained  -> the .NET runtime is bundled inside the exe.
REM   * PublishSingleFile + IncludeNativeLibrariesForSelfExtract is
REM     REQUIRED because Google OR-Tools ships native (.dll) code that
REM     is extracted next to a temp folder on first launch.
REM   * Trimming is intentionally NOT used - it breaks Avalonia's XAML
REM     reflection and OR-Tools' native bindings.
REM ===================================================================
setlocal
cd /d "%~dp0"

set PROJECT=src\SmartTimetable.Desktop\SmartTimetable.Desktop.csproj
set OUTDIR=publish

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo *** The .NET SDK was not found on PATH.
    echo *** Install .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo.
echo === Cleaning previous publish output ===
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"

echo.
echo === Publishing self-contained single-file exe (win-x64) ===
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "%OUTDIR%"
if errorlevel 1 goto :fail

echo.
echo ===================================================================
echo  PUBLISH SUCCEEDED
echo  Your installer is here:
echo     %CD%\%OUTDIR%\SmartTimetable.exe
echo.
echo  Give THIS single file to the college. On first launch it shows an
echo  activation code; generate a license with the LicenseGenerator tool
echo  and the college pastes the key back in to unlock it for life.
echo ===================================================================
start "" "%CD%\%OUTDIR%"
exit /b 0

:fail
echo.
echo *** PUBLISH FAILED - scroll up to see the first error. ***
exit /b 1
