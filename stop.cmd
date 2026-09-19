@echo off
setlocal

rem A running API or client holds Pf2e.Domain.dll open, and the next build fails with MSB3027
rem "the file is locked by". Stopping them is the fix, and it is common enough to be a script
rem rather than a thing to remember.
rem
rem Whoever is listening on the two ports IS the app, which is the one test that does not depend
rem on a process name. The client dev server runs as dotnet.exe, not Pf2e.Client.exe, so the
rem name list alone left it running and serving a stale build after every rebuild. The command
rem line match that used to cover it went through wmic, which is deprecated and absent on newer
rem Windows 11 builds, so it silently matched nothing.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ids = @(); " ^
  "foreach ($port in 5092,5173) { $ids += (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).OwningProcess }; " ^
  "$ids += (Get-Process -Name 'Pf2e.Api','Pf2e.Client' -ErrorAction SilentlyContinue).Id; " ^
  "$ids = $ids | Where-Object { $_ -and $_ -ne 0 } | Select-Object -Unique; " ^
  "foreach ($id in $ids) { $p = Get-Process -Id $id -ErrorAction SilentlyContinue; if ($p) { Write-Host \"Stopping $($p.ProcessName) ($id)\"; Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } }; " ^
  "Start-Sleep -Milliseconds 800; " ^
  "$left = @(); foreach ($port in 5092,5173) { $left += (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) }; " ^
  "if ($left.Count -gt 0) { Write-Host 'Something is still listening on 5092 or 5173.'; exit 1 }"

if errorlevel 1 (
  echo Could not free both ports. The next build may still fail with MSB3027.
  exit /b 1
)

echo Done. Nothing is holding the build output now.
exit /b 0
