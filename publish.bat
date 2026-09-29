@echo off
REM ===================================================================
REM  SmartTimetable AI  -  publish.bat
REM  Produces ONE self-contained SmartTimetable.exe for Windows x64.
REM  No .NET runtime needs to be installed on the college's PC.
REM
REM  This script does exactly ONE restore and ONE publish, then STOPS.
REM  If it ever looks like it "runs again and again":
REM    * The per-project "Restored / -> .dll" lines print once for EACH
REM      of the 6 projects - that is normal, it is still a single pass.
REM    * Do NOT run this from 'dotnet watch' or a VS Code / editor
REM      "run on save" task: publish writes files, a watcher sees those
REM      writes and re-launches it in a loop. Run it from a plain
REM      terminal (cmd) or by double-clicking the file.
REM
REM  Notes:
REM   * Self-contained  -> the .NET runtime is bundled inside the exe.
REM   * PublishSingleFile + IncludeNativeLibrariesForSelfExtract is
REM     REQUIRED because Google OR-Tools ships native (.dll) code.
REM   * Trimming is intentionally NOT used - it breaks Avalonia's XAML
REM     reflection and OR-Tools' native bindings.
REM   * Compression is OFF for speed/stability (the compress step is very
REM     CPU-heavy). The exe is larger but publishes much faster. To shrink
REM     it later, add  -p:EnableCompressionInSingleFile=true  below.
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
echo === [1/2] Restoring packages (once) ===
dotnet restore "%PROJECT%" -r win-x64 --nologo -v minimal
if errorlevel 1 goto :fail

echo.
echo === [2/2] Publishing self-contained single-file exe (win-x64) ===
echo     This is ONE long step - please wait, do not re-run it.
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    --no-restore ^
    --nologo ^
    -v minimal ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:SatelliteResourceLanguages=en ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "%OUTDIR%"
if errorlevel 1 goto :fail

echo.
echo ===================================================================
echo  PUBLISH SUCCEEDED - this window is now DONE (it will not re-run).
echo  Your app is:
echo     %CD%\%OUTDIR%\SmartTimetable.exe
echo.
echo  It sits in the "%OUTDIR%" folder next to a small support folder
echo  (LatoFont, used by PDF export). To hand the app over:
echo    * BEST: run build-installer.bat and give SmartTimetable-Setup.exe
echo      (it bundles everything into one installer), OR
echo    * zip the WHOLE "%OUTDIR%" folder and send that.
echo.
echo  Once your vendor public key is embedded (see docs\VENDOR_GUIDE.md),
echo  first launch shows an activation code.
echo ===================================================================
exit /b 0

:fail
echo.
echo *** PUBLISH FAILED - scroll up to the FIRST error line. ***
exit /b 1
