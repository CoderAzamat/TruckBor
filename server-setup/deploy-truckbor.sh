#!/bin/bash
# ╔══════════════════════════════════════════════╗
# ║  TruckBor — Serverda Deploy (bitta buyruq)  ║
# ║  Ishlatish: deploy-truckbor                  ║
# ╚══════════════════════════════════════════════╝
set -e

REPO_DIR="/root/TruckBor"
API_DIR="/var/www/truckbor/api"
WORKER_DIR="/var/www/truckbor/worker"
GITHUB_REPO="https://github.com/$(cd $REPO_DIR 2>/dev/null && git remote get-url origin 2>/dev/null | sed 's|.*github.com[:/]||;s|\.git$||' || echo 'YOUR_USER/TruckBor')"

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║        🚛 TruckBor Deploy Boshlandi          ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# ── 1. Git Pull ──────────────────────────────────────
echo ">>> [1/8] GitHub dan yangilash..."
if [ ! -d "$REPO_DIR" ]; then
    echo "    Repo topilmadi, clone qilinmoqda..."
    git clone "$GITHUB_REPO" "$REPO_DIR"
fi
cd "$REPO_DIR"
git fetch --all
git reset --hard origin/main 2>/dev/null || git reset --hard origin/master
echo "    ✅ Kod yangilandi: $(git log --oneline -1)"

# ── 2. Xizmatlarni to'xtatish ────────────────────────
echo ""
echo ">>> [2/8] Xizmatlar to'xtatilmoqda..."
systemctl stop truckbor        2>/dev/null || true
systemctl stop truckbor-worker 2>/dev/null || true
sleep 1

# ── 3. .NET Publish ──────────────────────────────────
echo ""
echo ">>> [3/8] API publish qilinmoqda..."
dotnet publish "$REPO_DIR/TruckBor.API/TruckBor.API.csproj" \
    -c Release -r linux-x64 --self-contained false \
    -o /tmp/truckbor-build/api 2>&1 | tail -3

echo ""
echo ">>> [4/8] Worker publish qilinmoqda..."
dotnet publish "$REPO_DIR/TruckBor.Worker/TruckBor.Worker.csproj" \
    -c Release -r linux-x64 --self-contained false \
    -o /tmp/truckbor-build/worker 2>&1 | tail -3

# ── 4. Mini App build (agar node bor bo'lsa) ─────────
echo ""
echo ">>> [5/8] Mini App..."
if [ -d "$REPO_DIR/TruckBor.MiniApp" ] && command -v npm &>/dev/null; then
    cd "$REPO_DIR/TruckBor.MiniApp"
    npm install --silent 2>/dev/null
    npm run build 2>&1 | tail -3
    echo "    ✅ Mini App build tayyor"
    cd "$REPO_DIR"
else
    echo "    ⚠️  Mini App o'tkazildi (npm yo'q yoki papka yo'q)"
fi

# ── 5. Fayllarni joylashtirish ────────────────────────
echo ""
echo ">>> [6/8] Fayllar joylashtirilmoqda..."
mkdir -p "$API_DIR" "$WORKER_DIR" "$API_DIR/wwwroot/uploads/logo"

rsync -a --delete \
    --exclude 'logs/' \
    --exclude 'wwwroot/uploads/' \
    --exclude 'appsettings.Production.json' \
    /tmp/truckbor-build/api/ "$API_DIR/"

rsync -a --delete \
    --exclude 'logs/' \
    /tmp/truckbor-build/worker/ "$WORKER_DIR/"

chown -R www-data:www-data /var/www/truckbor

# ── 6. Systemd + Nginx ───────────────────────────────
echo ""
echo ">>> [7/8] Konfiguratsiyalar yangilanmoqda..."

# Systemd service fayllar
cat > /etc/systemd/system/truckbor.service << 'SVCEOF'
[Unit]
Description=TruckBor API
After=network.target postgresql.service redis.service

[Service]
WorkingDirectory=/var/www/truckbor/api
ExecStart=/usr/bin/dotnet /var/www/truckbor/api/TruckBor.API.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=truckbor
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
SVCEOF

cat > /etc/systemd/system/truckbor-worker.service << 'SVCEOF'
[Unit]
Description=TruckBor Worker
After=network.target postgresql.service redis.service truckbor.service

[Service]
WorkingDirectory=/var/www/truckbor/worker
ExecStart=/usr/bin/dotnet /var/www/truckbor/worker/TruckBor.Worker.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=truckbor-worker
User=www-data
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
SVCEOF

systemctl daemon-reload

# Nginx
cp "$REPO_DIR/server-setup/nginx-truckbor.conf" /etc/nginx/sites-available/truckbor
ln -sf /etc/nginx/sites-available/truckbor /etc/nginx/sites-enabled/truckbor
rm -f /etc/nginx/sites-enabled/default

# Boshqa loyihalar truckbor domenlarini "o'g'irlamasligini" tekshirish
for f in /etc/nginx/sites-enabled/*; do
    [ "$f" = "/etc/nginx/sites-enabled/truckbor" ] && continue
    if grep -q "app\.truckbor\.uz\|api\.truckbor\.uz\|admin\.truckbor\.uz" "$f" 2>/dev/null; then
        echo "    ⚠️  $f da truckbor domeni topildi — link o'chirilmoqda"
        rm -f "$f"
    fi
done

nginx -t && systemctl reload nginx
echo "    ✅ Nginx yangilandi"

# ── 7. Xizmatlarni ishga tushirish ────────────────────
echo ""
echo ">>> [8/8] Xizmatlar ishga tushirilmoqda..."
systemctl enable truckbor truckbor-worker 2>/dev/null
systemctl start truckbor
sleep 4
systemctl start truckbor-worker

# ── Status ────────────────────────────────────────────
echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║                  STATUS                       ║"
echo "╠══════════════════════════════════════════════╣"

check_service() {
    if systemctl is-active --quiet "$1" 2>/dev/null; then
        echo "║  ✅ $2"
    else
        echo "║  ❌ $2 — XATO!"
        echo "║     → journalctl -u $1 -n 20"
    fi
}

check_service "truckbor"        "API (port 5000)           ║"
check_service "truckbor-worker" "Worker                    ║"
check_service "nginx"           "Nginx                     ║"
check_service "postgresql"      "PostgreSQL                ║"
check_service "redis-server"    "Redis                     ║"

echo "╠══════════════════════════════════════════════╣"

sleep 2
HTTP=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null || echo "000")
if [ "$HTTP" = "200" ]; then
    echo "║  ✅ Health Check — OK (200)              ║"
else
    echo "║  ❌ Health Check — XATO ($HTTP)              ║"
    echo "║  → journalctl -u truckbor -n 30           ║"
fi

echo "╚══════════════════════════════════════════════╝"
echo ""
echo "  Commit: $(cd $REPO_DIR && git log --oneline -1)"
echo ""
echo "  🌐 API:     https://api.truckbor.uz"
echo "  📱 MiniApp: https://app.truckbor.uz"
echo "  🖥️  Admin:  https://admin.truckbor.uz"
echo ""

# Tozalash
rm -rf /tmp/truckbor-build
