# CS2 Tactical Assistant — start HLTV bridge + ASP.NET API in separate windows.
# Usage:  pwsh -File .\start-dev.ps1
#         pwsh -File .\start-dev.ps1 -KillOldApi   # stops a running API so dotnet can rebuild
param([switch]$KillOldApi)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if ($KillOldApi) {
  Get-Process -Name "CS2TacticalAssistant.Api" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping old API (PID $($_.Id))…" -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force
  }
  Start-Sleep -Milliseconds 500
}
$bridge = Join-Path $root "hltv-bridge"
$api = Join-Path $root "CS2TacticalAssistant.Api"

if (-not (Test-Path (Join-Path $bridge "server.mjs"))) {
  Write-Error "Run this script from the repo root (hltv-bridge not found)."
  exit 1
}

Write-Host "Starting hltv-bridge on http://127.0.0.1:3847 (new window)…" -ForegroundColor Cyan
Start-Process -FilePath "cmd.exe" -ArgumentList @(
  "/k", "cd /d `"$bridge`" && npm start"
) -WindowStyle Normal

Start-Sleep -Seconds 1

Write-Host "Starting Kestrel API (new window) — then open http://localhost:5168" -ForegroundColor Cyan
Start-Process -FilePath "cmd.exe" -ArgumentList @(
  "/k", "cd /d `"$api`" && dotnet run"
) -WindowStyle Normal

Write-Host ""
Write-Host "If port 3847 is already in use, close the other hltv-bridge window first." -ForegroundColor Yellow
Write-Host "If the API was already running, stop it so `dotnet run` can rebuild and load the latest /api routes." -ForegroundColor Yellow
Write-Host ""
Write-Host "Open: http://localhost:5168" -ForegroundColor Green
