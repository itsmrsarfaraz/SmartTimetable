@echo off
REM ===================================================================
REM  SmartTimetable AI  -  license.bat   (VENDOR ONLY - keep private)
REM  Product by Crafting Colons
REM
REM  A thin wrapper around the LicenseGenerator tool so you never have
REM  to type the long "dotnet run --project ..." each time.
REM
REM  Run it ONCE to create your signing key:
REM      license.bat keygen
REM
REM  Then, for each college that sends you an activation code:
REM      license.bat issue --request THEIR-CODE --customer "Green Valley College"
REM
REM  Other commands:
REM      license.bat decode --request THEIR-CODE
REM      license.bat issue  --request THEIR-CODE --customer "X" --expires 2027-12-31
REM      license.bat verify --license SmartTimetable-X-1a2b3c4d.lic
REM
REM  NEVER share this tool, vendor_private.pem, or your passphrase with
REM  a customer. They only ever receive the .exe and the license key.
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

dotnet run --project "tools\SmartTimetable.LicenseGenerator" -c Release -- %*
exit /b %errorlevel%
