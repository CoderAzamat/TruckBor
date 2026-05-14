#!/bin/bash
# ╔══════════════════════════════════════════════════╗
# ║  TruckBor — Serverga birinchi marta o'rnatish    ║
# ║  BU FAQAT 1 MARTA ishlatiladi!                   ║
# ║  Ishlatish:                                       ║
# ║    bash first-setup.sh https://github.com/U/T.git║
# ╚══════════════════════════════════════════════════╝
set -e

echo "╔══════════════════════════════════════════════╗"
echo "║    TruckBor — Server Setup Boshlandi          ║"
echo "╚══════════════════════════════════════════════╝"

# ── 1. GitHub repo clone ──────────────────────────────
echo ""
echo ">>> [1/7] GitHub repo clone qilinmoqda..."
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

# ── 2. System paketlari ──────────────────────────────
echo ""
echo ">>> [2/7] Tizim paketlari o'rnatilmoqda..."
apt-get update -qq
apt-get install -y -qq \
    python3 python3-pip python3-venv \
    certbot python3-certbot-nginx \
    nginx curl git rsync 2>&1 | tail -3
echo "    ✅ Paketlar tayyor"

# ── 3. deploy-truckbor buyrug'ini o'rnatish ───────────
echo ""
echo ">>> [3/7] deploy-truckbor buyrug'i o'rnatilmoqda..."
cp /root/TruckBor/server-setup/deploy-truckbor.sh /usr/local/bin/deploy-truckbor
chmod +x /usr/local/bin/deploy-truckbor
echo "    ✅ Endi istalgan joyda 'deploy-truckbor' yozsangiz ishlaydi"

# ── 4. TG_API_ID va TG_API_HASH ──────────────────────
echo ""
echo ">>> [4/7] Telegram API sozlamalari..."
echo ""
echo "  my.telegram.org dan API_ID va API_HASH oling:"
echo "  1. https://my.telegram.org ga kiring"
echo "  2. 'API development tools' bo'limini oching"
echo "  3. Yangi app yarating (App title: TruckBor, Platform: Other)"
echo ""

PYRO_ENV_FILE="/etc/truckbor-pyro.env"

read -r -p "  TG_API_ID (raqam): " TG_API_ID
read -r -p "  TG_API_HASH (string): " TG_API_HASH

if [ -n "$TG_API_ID" ] && [ -n "$TG_API_HASH" ]; then
    cat > "$PYRO_ENV_FILE" << EOF
TG_API_ID=${TG_API_ID}
TG_API_HASH=${TG_API_HASH}
SESSION_DIR=/var/www/truckbor/sessions
PORT=5050
EOF
    chmod 600 "$PYRO_ENV_FILE"
    echo "    ✅ API kalitlar saqlandi: $PYRO_ENV_FILE"
else
    cat > "$PYRO_ENV_FILE" << 'EOF'
TG_API_ID=0
TG_API_HASH=
SESSION_DIR=/var/www/truckbor/sessions
PORT=5050
EOF
    chmod 600 "$PYRO_ENV_FILE"
    echo "    ⚠️  Bo'sh qoldirildi. Keyinroq: nano $PYRO_ENV_FILE"
fi

# ── 5. SSL sertifikat ─────────────────────────────────
echo ""
echo ">>> [5/7] SSL sertifikat (Let's Encrypt)..."

# Avval nginx da oddiy config qo'yish (SSL olish uchun)
cat > /etc/nginx/sites-available/truckbor-temp << 'EOF'
server {
    listen 80;
    server_name api.truckbor.uz app.truckbor.uz admin.truckbor.uz truckbor.uz;
    root /var/www/html;
    location /.well-known/acme-challenge/ { root /var/www/html; }
    location / { return 200 'ok'; add_header Content-Type text/plain; }
}
EOF
ln -sf /etc/nginx/sites-available/truckbor-temp /etc/nginx/sites-enabled/truckbor-temp
# Boshqa konflikting nginx configlarni o'chirish
for f in /etc/nginx/sites-enabled/*; do
    [ "$f" = "/etc/nginx/sites-enabled/truckbor-temp" ] && continue
    if grep -q "app\.truckbor\.uz\|api\.truckbor\.uz\|admin\.truckbor\.uz" "$f" 2>/dev/null; then
        echo "    Konflikt: $f — o'chirilmoqda"
        rm -f "$f"
    fi
done
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx 2>/dev/null || systemctl start nginx

echo "    SSL sertifikat olinmoqda (3 domen)..."
certbot certonly --nginx \
    -d api.truckbor.uz \
    -d app.truckbor.uz \
    -d admin.truckbor.uz \
    --non-interactive --agree-tos \
    --email dd1778673@gmail.com \
    2>&1 | tail -5 \
    || echo "    ⚠️ SSL xatosi — qo'lda: certbot certonly --nginx -d api.truckbor.uz -d app.truckbor.uz -d admin.truckbor.uz"

rm -f /etc/nginx/sites-enabled/truckbor-temp
rm -f /etc/nginx/sites-available/truckbor-temp

echo "    ✅ SSL tayyor"

# ── 6. appsettings.Production.json ────────────────────
echo ""
echo ">>> [6/7] Production konfiguratsiya..."
API_DIR="/var/www/truckbor/api"
WORKER_DIR="/var/www/truckbor/worker"
SESSIONS_DIR="/var/www/truckbor/sessions"
mkdir -p "$API_DIR" "$WORKER_DIR" "$SESSIONS_DIR"

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
else
    echo "    appsettings.Production.json allaqachon bor"
fi

if [ ! -f "$WORKER_DIR/appsettings.Production.json" ]; then
    cp "$API_DIR/appsettings.Production.json" "$WORKER_DIR/appsettings.Production.json"
fi

# ── 7. PostgreSQL DB yaratish ─────────────────────────
echo ""
echo ">>> [7/7] Database..."
sudo -u postgres psql -lqt 2>/dev/null | cut -d \| -f 1 | grep -qw truckbor || \
    sudo -u postgres createdb truckbor 2>/dev/null || \
    echo "    ⚠️  DB allaqachon bor yoki PostgreSQL ishlamaydi"
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
echo "  1. Windows: .\\deploy.ps1"
echo "  2. SSH:     deploy-truckbor"
echo ""
echo "  pyro-auth (Telegram login) sozlash:"
echo "  nano /etc/truckbor-pyro.env       ← API kalitlarni yozing"
echo "  systemctl restart truckbor-pyro   ← Qayta ishga tushiring"
echo ""
