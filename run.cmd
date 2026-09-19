@echo off
setlocal

rem Starts both halves of the app. Running the client alone shows a rules browser that cannot
rem reach anything, because the client is a separate deployable that talks to the API over HTTP.
rem That is the architecture, not a bug, but it does mean one project is never enough.

cd /d "%~dp0"

rem A previous run holds the build output open, so stop it before doing anything else.
call "%~dp0stop.cmd" >nul 2>&1

if not exist "tools\rules-import\out\seed\class.json" (
  echo Seed data is missing, generating it from the tracked snapshot...
  dotnet run --project tools\rules-import -- transform
  if errorlevel 1 (
    echo.
    echo Could not generate seed data. The API will refuse to start without it.
    exit /b 1
  )
)

echo Starting the API on http://localhost:5092
start "Pf2e API" cmd /c "dotnet run --project src\Pf2e.Api --urls http://localhost:5092"

echo Starting the client on http://localhost:5173
start "Pf2e Client" cmd /c "dotnet run --project src\Pf2e.Client --launch-profile http"

echo.
echo Waiting for the API to come up...
set /a tries=0
:wait
set /a tries+=1
timeout /t 2 /nobreak >nul
curl -s -o nul -m 2 http://localhost:5092/health && goto ready
if %tries% lss 45 goto wait
echo The API did not come up. Look at the "Pf2e API" window for the reason.
exit /b 1

:ready
echo.
echo   API      http://localhost:5092/health
echo   Client   http://localhost:5173
echo.
start "" http://localhost:5173
echo Both windows stay open. Close them to stop.
