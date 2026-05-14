#!/bin/bash
# ╔══════════════════════════════════════════════════╗
# ║  TruckBor — Serverga birinchi marta o'rnatish    ║
# ║  BU FAQAT 1 MARTA ishlatiladi!                   ║
# ╚══════════════════════════════════════════════════╝
set -e

echo "╔══════════════════════════════════════════════╗"
echo "║    TruckBor — Server Setup Boshlandi          ║"
echo "╚══════════════════════════════════════════════╝"

# ── 1. GitHub repo clone ──────────────────────────────
echo ""
echo ">>> [1/5] GitHub repo clone qilinmoqda..."
REPO_URL="${1:-}"
if [ -z "$REPO_URL" ]; then
    echo "❌ GitHub repo URL kiriting!"
    echo "   Ishlatish: bash first-setup.sh https://github.com/USERNAME/TruckBor.git"
    exit 1
fi

if [ -d "/root/TruckBor" ]; then
    echo "    /root/TruckBor allaqachon bor — yangilanmoqda..."
    cd /root/TruckBor
    git pull
else
    git clone "$REPO_URL" /root/TruckBor
fi

# ── 2. deploy-truckbor buyrug'ini o'rnatish ───────────
echo ""
echo ">>> [2/5] deploy-truckbor buyrug'i o'rnatilmoqda..."
cp /root/TruckBor/server-setup/deploy-truckbor.sh /usr/local/bin/deploy-truckbor
chmod +x /usr/local/bin/deploy-truckbor
echo "    ✅ Endi istalgan joyda 'deploy-truckbor' yozsangiz ishlaydi"

# ── 3. SSL sertifikat ─────────────────────────────────
echo ""
echo ">>> [3/5] SSL sertifikat..."
if ! command -v certbot &>/dev/null; then
    apt update && apt install -y certbot python3-certbot-nginx
fi

# Avval nginx da oddiy config qo'yish (SSL olish uchun)
cat > /etc/nginx/sites-available/truckbor-temp << 'EOF'
server {
    listen 80;
    server_name api.truckbor.uz app.truckbor.uz admin.truckbor.uz truckbor.uz;
    root /var/www/html;
    location / { return 200 'ok'; }
}
EOF
ln -sf /etc/nginx/sites-available/truckbor-temp /etc/nginx/sites-enabled/truckbor-temp
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx

echo "    SSL sertifikat olinmoqda..."
certbot certonly --nginx \
    -d api.truckbor.uz \
    -d app.truckbor.uz \
    -d admin.truckbor.uz \
    --non-interactive --agree-tos \
    --email dd1778673@gmail.com || echo "    ⚠️ SSL xatosi — qo'lda ishlatib ko'ring"

rm -f /etc/nginx/sites-enabled/truckbor-temp
rm -f /etc/nginx/sites-available/truckbor-temp

# ── 4. appsettings.Production.json ────────────────────
echo ""
echo ">>> [4/5] Production konfiguratsiya..."
API_DIR="/var/www/truckbor/api"
WORKER_DIR="/var/www/truckbor/worker"
mkdir -p "$API_DIR" "$WORKER_DIR"

if [ ! -f "$API_DIR/appsettings.Production.json" ]; then
    cat > "$API_DIR/appsettings.Production.json" << 'EOF'
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=truckbor;Username=postgres;Password=azamat2009k",
    "Redis": "localhost:6379"
  },
  "Bot": {
    "Token": "8676920473:AAG0hs3Ml2C0mrqF5kVVVWRFJ-6eFrHcZ-4",
    "WebhookUrl": "https://api.truckbor.uz/api/webhook",
    "AdminIds": [ 8005080354 ]
  },
  "TelegramAuth": {
    "ServiceUrl": "http://localhost:5050"
  },
  "Jwt": {
    "Secret": "truckbor-super-secret-key-min-32-chars-2024!!",
    "Issuer": "TruckBor",
    "Audience": "TruckBorUsers"
  }
}
EOF
    echo "    ✅ appsettings.Production.json yaratildi"
    echo "    ⚠️  /var/www/truckbor/api/appsettings.Production.json ni tekshiring!"
else
    echo "    appsettings.Production.json allaqachon bor"
fi

# Worker uchun ham
if [ ! -f "$WORKER_DIR/appsettings.Production.json" ]; then
    cp "$API_DIR/appsettings.Production.json" "$WORKER_DIR/appsettings.Production.json"
fi

# ── 5. PostgreSQL DB yaratish ─────────────────────────
echo ""
echo ">>> [5/5] Database..."
sudo -u postgres psql -lqt | cut -d \| -f 1 | grep -qw truckbor || \
    sudo -u postgres createdb truckbor
echo "    ✅ truckbor database tayyor"

# ── BIRINCHI DEPLOY ──────────────────────────────────
echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║    Setup tayyor! Birinchi deploy boshlandi    ║"
echo "╚══════════════════════════════════════════════╝"
echo ""
deploy-truckbor

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "  ✅ HAMMASI TAYYOR!"
echo ""
echo "  Keyingi safar deploy qilish uchun:"
echo "  1. Windows: git push"
echo "  2. SSH:     deploy-truckbor"
echo ""
echo "  Yoki bitta buyruq bilan (Windows PowerShell):"
echo "  .\\deploy.ps1"
echo ""
