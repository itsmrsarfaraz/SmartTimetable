@echo off
REM ===================================================================
REM  SmartTimetable AI  -  build-installer.bat
REM  Product by Crafting Colons
REM
REM  Produces installer-output\SmartTimetable-Setup.exe, a proper
REM  Windows installer with Start-Menu shortcuts and an uninstaller
REM  whose icon is TimetableDelete.ico.
REM
REM  It will publish the app first if publish\SmartTimetable.exe is
REM  missing, then compile installer\SmartTimetable.iss with Inno Setup.
REM ===================================================================
setlocal
cd /d "%~dp0"

REM --- 1) Make sure the app has been published -----------------------
if not exist "publish\SmartTimetable.exe" (
    echo.
    echo === No published exe found - running publish.bat first ===
    call publish.bat
    if errorlevel 1 (
        echo *** Publish failed - fix the build error above and re-run. ***
        exit /b 1
    )
)

REM --- 2) Make sure the icon files are in place ----------------------
if not exist "src\SmartTimetable.Desktop\Assets\timetable.ico" (
    echo.
    echo *** Missing: src\SmartTimetable.Desktop\Assets\timetable.ico
    echo *** Copy your timetable.ico there, then re-run.
    exit /b 1
)
if not exist "installer\TimetableDelete.ico" (
    echo.
    echo *** Missing: installer\TimetableDelete.ico
    echo *** Copy your TimetableDelete.ico there, then re-run.
    exit /b 1
)

REM --- 3) Locate the Inno Setup compiler (ISCC.exe) ------------------
set "ISCC="
where iscc >nul 2>nul && set "ISCC=iscc"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not defined ISCC (
    echo.
    echo *** Inno Setup ^(ISCC.exe^) was not found.
    echo *** Install Inno Setup 6 from https://jrsoftware.org/isdl.php and re-run.
    exit /b 1
)

REM --- 4) Compile the installer -------------------------------------
echo.
echo === Compiling installer with Inno Setup ===
"%ISCC%" "installer\SmartTimetable.iss"
if errorlevel 1 (
    echo.
    echo *** Installer build FAILED - scroll up for the first error. ***
    exit /b 1
)

echo.
echo ===================================================================
echo  INSTALLER BUILD SUCCEEDED
echo  Your installer is here:
echo     %CD%\installer-output\SmartTimetable-Setup.exe
echo.
echo  Give THIS file to the college. Installing it registers an
echo  uninstall entry that uses TimetableDelete.ico.
echo ===================================================================
if exist "installer-output" start "" "%CD%\installer-output"
exit /b 0
