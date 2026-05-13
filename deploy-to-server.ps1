# TruckBor — Windows'dan Serverga Deploy
# Ishlatish: .\deploy-to-server.ps1
# Talab: OpenSSH yoki PuTTY o'rnatilgan bo'lishi kerak

param(
    [string]$ServerIP   = "144.91.118.230",
    [string]$ServerUser = "root",
    [string]$SshKey     = ""   # bo'sh bo'lsa parol so'raladi
)

$ErrorActionPreference = "Stop"
$SshOpts = if ($SshKey) { "-i `"$SshKey`"" } else { "" }

Write-Host "=== TruckBor Deploy ===" -ForegroundColor Cyan
Write-Host "Server: $ServerUser@$ServerIP" -ForegroundColor Yellow

# ── 1. Publish ──────────────────────────────────
Write-Host "`n>>> .NET publish..." -ForegroundColor Green

Set-Location "C:\Projects\TruckBor"

dotnet publish TruckBor.API/TruckBor.API.csproj `
    -c Release -r linux-x64 --self-contained false `
    -o publish/api

dotnet publish TruckBor.Worker/TruckBor.Worker.csproj `
    -c Release -r linux-x64 --self-contained false `
    -o publish/worker

# ── 2. Mini App build ───────────────────────────
Write-Host "`n>>> Mini App build..." -ForegroundColor Green
Set-Location "C:\Projects\TruckBor\TruckBor.MiniApp"
npm run build
Set-Location "C:\Projects\TruckBor"

# ── 3. Serverda papka tayyorlash ────────────────
Write-Host "`n>>> Server papkalar..." -ForegroundColor Green
$cmd = "mkdir -p /tmp/truckbor/api /tmp/truckbor/worker"
ssh $SshOpts "$ServerUser@$ServerIP" $cmd

# ── 4. Fayllarni yuborish (scp) ─────────────────
Write-Host "`n>>> Fayllar yuklanmoqda (bu biroz vaqt oladi)..." -ForegroundColor Green

# Service va config fayllar
scp $SshOpts publish/truckbor-api.service    "${ServerUser}@${ServerIP}:/tmp/truckbor/"
scp $SshOpts publish/truckbor-worker.service "${ServerUser}@${ServerIP}:/tmp/truckbor/"
scp $SshOpts publish/nginx-truckbor.conf     "${ServerUser}@${ServerIP}:/tmp/truckbor/"
scp $SshOpts publish/deploy.sh               "${ServerUser}@${ServerIP}:/tmp/truckbor/"

# API fayllar
Write-Host "    API fayllar..." -ForegroundColor DarkGray
scp $SshOpts -r publish/api/* "${ServerUser}@${ServerIP}:/tmp/truckbor/api/"

# Worker fayllar
Write-Host "    Worker fayllar..." -ForegroundColor DarkGray
scp $SshOpts -r publish/worker/* "${ServerUser}@${ServerIP}:/tmp/truckbor/worker/"

# ── 5. Serverda deploy ishga tushirish ──────────
Write-Host "`n>>> Deploy ishga tushirilmoqda..." -ForegroundColor Green
ssh $SshOpts "$ServerUser@$ServerIP" "chmod +x /tmp/truckbor/deploy.sh && bash /tmp/truckbor/deploy.sh"

Write-Host "`n✅ Deploy MUVAFFAQIYATLI!" -ForegroundColor Green
Write-Host "🌐 API:     https://api.truckbor.uz" -ForegroundColor Cyan
Write-Host "🤖 Webhook: https://api.truckbor.uz/api/webhook" -ForegroundColor Cyan
Write-Host "📱 MiniApp: https://api.truckbor.uz/miniapp" -ForegroundColor Cyan
