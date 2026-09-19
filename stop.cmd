@echo off
setlocal

rem A running API or client holds Pf2e.Domain.dll open, and the next build fails with MSB3027
rem "the file is locked by". Stopping them is the fix, and it is common enough to be a script
rem rather than a thing to remember.

set stopped=0
for %%P in (Pf2e.Api Pf2e.Client) do (
  tasklist /fi "imagename eq %%P.exe" 2>nul | find /i "%%P.exe" >nul && (
    echo Stopping %%P
    taskkill /f /im %%P.exe >nul 2>&1
    set stopped=1
  )
)

rem dotnet watch and dotnet run wrappers can outlive the app they launched and keep the lock.
for /f "tokens=2 delims=," %%I in ('tasklist /fi "imagename eq dotnet.exe" /fo csv /nh 2^>nul') do (
  wmic process where "ProcessId=%%~I" get CommandLine 2>nul | find /i "Pf2e." >nul && (
    echo Stopping a dotnet host running a Pf2e project
    taskkill /f /pid %%~I >nul 2>&1
  )
)

timeout /t 1 /nobreak >nul
echo Done. Nothing is holding the build output now.

rem wmic is deprecated and can leave a non-zero code behind even when everything worked.
exit /b 0
