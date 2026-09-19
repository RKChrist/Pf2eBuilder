@echo off
setlocal

rem Serves the whole app from ONE process and ONE port, which is what an ngrok tunnel needs.
rem The API hosts the published client, so phones and desktops share an origin and there is no
rem CORS to configure and no second tunnel to pay for.
rem
rem   serve.cmd            serve on http://localhost:5092
rem   serve.cmd 8080       serve on http://localhost:8080
rem
rem Then, in another window:  ngrok http 5092
rem Open the https URL ngrok prints on every phone and every desktop at the table.

cd /d "%~dp0"
call "%~dp0stop.cmd" >nul 2>&1

set PORT=%1
if "%PORT%"=="" set PORT=5092
set PUBLISH=%~dp0artifacts\client

if not exist "tools\rules-import\out\seed\class.json" (
  echo Generating seed data from the tracked snapshot...
  dotnet run --project tools\rules-import -- transform || exit /b 1
)

echo Publishing the client...
dotnet publish src\Pf2e.Client -c Release -o "%PUBLISH%" --nologo || exit /b 1

echo.
echo Serving both halves on http://localhost:%PORT%
echo Expose it with:  ngrok http %PORT%
echo.

rem ClientRoot is where the published WebAssembly app lives. The API serves it and falls back to
rem index.html so a deep link into the client does not 404 on refresh.
set Hosting__ClientRoot=%PUBLISH%\wwwroot
dotnet run --project src\Pf2e.Api -c Release --urls http://0.0.0.0:%PORT%
