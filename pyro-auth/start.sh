#!/bin/bash
# Pyrogram Auth Microservice — server setup
# Run once: bash start.sh
# Then set TelegramAuth:ServiceUrl = http://localhost:5050 in appsettings

pip3 install -r requirements.txt

export TG_API_ID="YOUR_API_ID"        # https://my.telegram.org
export TG_API_HASH="YOUR_API_HASH"
export SESSION_DIR="/var/www/truckbor/sessions"
export PORT=5050

mkdir -p $SESSION_DIR

# Run with gunicorn in production
pip3 install gunicorn
gunicorn -w 1 -b 0.0.0.0:5050 main:app --log-level info
