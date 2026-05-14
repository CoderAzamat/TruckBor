# TruckBor — Bitta buyruq bilan deploy
# Ishlatish: .\deploy.ps1
# git push + serverda deploy-truckbor

$Server = "root@144.91.118.230"

Write-Host "`n=== TruckBor Deploy ===" -ForegroundColor Cyan

# 1. Git push
Write-Host "`n>>> Git push..." -ForegroundColor Green
Set-Location "C:\Projects\TruckBor"
git add .
$commitMsg = Read-Host "Commit xabar (Enter = 'update')"
if (-not $commitMsg) { $commitMsg = "update" }
git commit -m $commitMsg 2>$null
git push

# 2. SSH da deploy
Write-Host "`n>>> Serverda deploy boshlandi..." -ForegroundColor Green
ssh $Server "deploy-truckbor"

Write-Host "`n=== TAYYOR! ===" -ForegroundColor Green
Write-Host "API:     https://api.truckbor.uz" -ForegroundColor Cyan
Write-Host "MiniApp: https://app.truckbor.uz" -ForegroundColor Cyan
Write-Host "Admin:   https://admin.truckbor.uz" -ForegroundColor Cyan
