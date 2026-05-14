#!/bin/bash
# ╔══════════════════════════════════════════════╗
# ║  TruckBor — Serverda Deploy (bitta buyruq)  ║
# ║  Ishlatish: deploy-truckbor                  ║
# ╚══════════════════════════════════════════════╝
set -e

REPO_DIR="/root/TruckBor"
API_DIR="/var/www/truckbor/api"
WORKER_DIR="/var/www/truckbor/worker"
PYRO_DIR="/var/www/truckbor/pyro-auth"
SESSIONS_DIR="/var/www/truckbor/sessions"
GITHUB_REPO="https://github.com/$(cd $REPO_DIR 2>/dev/null && git remote get-url origin 2>/dev/null | sed 's|.*github.com[:/]||;s|\.git$||' || echo 'YOUR_USER/TruckBor')"

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║        🚛 TruckBor Deploy Boshlandi          ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# ── 1. Git Pull ──────────────────────────────────────
echo ">>> [1/9] GitHub dan yangilash..."
if [ ! -d "$REPO_DIR" ]; then
    echo "    Repo topilmadi, clone qilinmoqda..."
    git clone "$GITHUB_REPO" "$REPO_DIR"
fi
cd "$REPO_DIR"
git fetch --all
git reset --hard origin/main 2>/dev/null || git reset --hard origin/master
echo "    ✅ Kod yangilandi: $(git log --oneline -1)"

# Deploy scriptni ham yangilaymiz (keyingi safar uchun)
if [ -f "$REPO_DIR/server-setup/deploy-truckbor.sh" ]; then
    cp "$REPO_DIR/server-setup/deploy-truckbor.sh" /usr/local/bin/deploy-truckbor
    chmod +x /usr/local/bin/deploy-truckbor
fi

# ── 2. Xizmatlarni to'xtatish ────────────────────────
echo ""
echo ">>> [2/9] Xizmatlar to'xtatilmoqda..."
systemctl stop truckbor        2>/dev/null || true
systemctl stop truckbor-worker 2>/dev/null || true
systemctl stop truckbor-pyro   2>/dev/null || true
sleep 1

# ── 3. Mini App build (dotnet publish dan OLDIN — wwwroot/miniapp ga ketadi) ──
echo ""
echo ">>> [3/9] Mini App build..."
if [ -d "$REPO_DIR/TruckBor.MiniApp" ] && command -v npm &>/dev/null; then
    cd "$REPO_DIR/TruckBor.MiniApp"
    npm install --silent 2>/dev/null
    npm run build 2>&1 | tail -3
    echo "    ✅ Mini App build tayyor (wwwroot/miniapp)"
    cd "$REPO_DIR"
else
    echo "    ⚠️  Mini App o'tkazildi (npm yo'q yoki papka yo'q)"
fi

# ── 4. .NET Publish (miniapp build tayyor bo'lgandan keyin) ───────────────────
echo ""
echo ">>> [4/9] API publish qilinmoqda..."
dotnet publish "$REPO_DIR/TruckBor.API/TruckBor.API.csproj" \
    -c Release -r linux-x64 --self-contained false \
    -o /tmp/truckbor-build/api 2>&1 | tail -3

echo ""
echo ">>> [5/9] Worker publish qilinmoqda..."
dotnet publish "$REPO_DIR/TruckBor.Worker/TruckBor.Worker.csproj" \
    -c Release -r linux-x64 --self-contained false \
    -o /tmp/truckbor-build/worker 2>&1 | tail -3

# ── 5. Fayllarni joylashtirish ────────────────────────
echo ""
echo ">>> [6/9] Fayllar joylashtirilmoqda..."
mkdir -p "$API_DIR" "$WORKER_DIR" "$PYRO_DIR" "$SESSIONS_DIR"
mkdir -p "$API_DIR/wwwroot/uploads/logo"

rsync -a --delete \
    --exclude 'logs/' \
    --exclude 'wwwroot/uploads/' \
    --exclude 'appsettings.Production.json' \
    /tmp/truckbor-build/api/ "$API_DIR/"

rsync -a --delete \
    --exclude 'logs/' \
    /tmp/truckbor-build/worker/ "$WORKER_DIR/"

# pyro-auth fayllar
cp "$REPO_DIR/pyro-auth/main.py"          "$PYRO_DIR/"
cp "$REPO_DIR/pyro-auth/requirements.txt" "$PYRO_DIR/"

chown -R www-data:www-data /var/www/truckbor

# ── 6. pyro-auth dependencies ────────────────────────
echo ""
echo ">>> [7/9] Python dependencies..."
if command -v pip3 &>/dev/null; then
    pip3 install -q -r "$PYRO_DIR/requirements.txt" 2>&1 | tail -2
    echo "    ✅ Python packages yangilandi"
else
    echo "    ⚠️  pip3 topilmadi — pyro-auth ishlamaydi"
fi

# ── 7. Systemd + Nginx ───────────────────────────────
echo ""
echo ">>> [8/9] Konfiguratsiyalar yangilanmoqda..."

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

# pyro-auth service — TG_API_ID va TG_API_HASH saqlanadi
PYRO_ENV_FILE="/etc/truckbor-pyro.env"
if [ ! -f "$PYRO_ENV_FILE" ]; then
    cat > "$PYRO_ENV_FILE" << 'ENVEOF'
TG_API_ID=0
TG_API_HASH=
SESSION_DIR=/var/www/truckbor/sessions
PORT=5050
ENVEOF
    chmod 600 "$PYRO_ENV_FILE"
    echo "    ⚠️  $PYRO_ENV_FILE ga TG_API_ID va TG_API_HASH yozing!"
fi

cat > /etc/systemd/system/truckbor-pyro.service << 'SVCEOF'
[Unit]
Description=TruckBor Pyrogram Auth Service
After=network.target

[Service]
WorkingDirectory=/var/www/truckbor/pyro-auth
ExecStart=/usr/bin/python3 -m gunicorn -w 1 -b 0.0.0.0:5050 main:app --timeout 120 --log-level info
Restart=always
RestartSec=5
KillSignal=SIGTERM
SyslogIdentifier=truckbor-pyro
User=www-data
EnvironmentFile=/etc/truckbor-pyro.env

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

# ── 8. Xizmatlarni ishga tushirish ────────────────────
echo ""
echo ">>> [9/9] Xizmatlar ishga tushirilmoqda..."
systemctl enable truckbor truckbor-worker truckbor-pyro 2>/dev/null
systemctl start truckbor
sleep 4
systemctl start truckbor-worker

# pyro-auth faqat API_ID sozlangan bo'lsa ishga tushadi
PYRO_API_ID=$(grep TG_API_ID "$PYRO_ENV_FILE" 2>/dev/null | cut -d= -f2)
if [ "$PYRO_API_ID" != "0" ] && [ -n "$PYRO_API_ID" ]; then
    systemctl start truckbor-pyro
else
    echo "    ⚠️  pyro-auth o'chirildi (TG_API_ID=0). Sozlash uchun:"
    echo "    nano $PYRO_ENV_FILE"
    echo "    systemctl start truckbor-pyro"
fi

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
check_service "truckbor-pyro"   "Pyro-Auth (port 5050)     ║"
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
