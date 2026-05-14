# TruckBor — Bitta buyruq bilan deploy
# Ishlatish: .\deploy.ps1
# git push + serverda deploy-truckbor

param(
    [string]$Server    = "root@144.91.118.230",
    [string]$CommitMsg = ""
)

Write-Host "`n╔════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   🚛 TruckBor Deploy           ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════╝`n" -ForegroundColor Cyan

# 1. Git push
Write-Host ">>> Git push..." -ForegroundColor Green
Set-Location "C:\Projects\TruckBor"
git add .
if (-not $CommitMsg) {
    $CommitMsg = Read-Host "Commit xabar (Enter = 'update')"
    if (-not $CommitMsg) { $CommitMsg = "update" }
}
$hasChanges = git status --porcelain
if ($hasChanges) {
    git commit -m $CommitMsg
} else {
    Write-Host "    (o'zgarish yo'q, push qilinadi)" -ForegroundColor DarkGray
}
git push

# 2. SSH da deploy
Write-Host "`n>>> Serverda deploy boshlandi..." -ForegroundColor Green
ssh $Server "deploy-truckbor"

Write-Host "`n╔════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   ✅ DEPLOY MUVAFFAQIYATLI!    ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════╝" -ForegroundColor Green
Write-Host "  🌐 API:     https://api.truckbor.uz" -ForegroundColor Cyan
Write-Host "  📱 MiniApp: https://app.truckbor.uz" -ForegroundColor Cyan
Write-Host "  🖥️  Admin:  https://admin.truckbor.uz`n" -ForegroundColor Cyan
