@echo off
title Rent (HTTPS)
setlocal
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Development

REM Adres strony startowej (menu g³ówne)
set URL_HTTPS=https://localhost:7177/

echo Ensuring HTTPS dev certificate is trusted (one-time action)...
dotnet dev-certs https --trust >nul2>&1

echo Starting Rent with launch profile "Rent"...
start "Rent - backend" cmd /c dotnet run --project "WebApplication2\Rent.csproj" --launch-profile "Rent"

echo Waiting for app to be ready on %URL_HTTPS% ...
powershell -NoProfile -Command ^
 "$u='%URL_HTTPS%'; $deadline=(Get-Date).AddSeconds(30); while((Get-Date) -lt $deadline){ try { $r=Invoke-WebRequest -UseBasicParsing -Uri $u -TimeoutSec 2; if($r.StatusCode -ge 200){ Start-Process $u; break } } catch { } Start-Sleep -Milliseconds 500 }"

endlocal
exit /b