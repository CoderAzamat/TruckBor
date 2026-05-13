"""
TruckBor — Pyrogram SMS Auth Microservice
Run:  pip install pyrogram tgcrypto flask && python main.py
Port: 5050 (configure TelegramAuth:ServiceUrl = http://localhost:5050)
"""

import os, json, logging
from flask import Flask, request, jsonify
from pyrogram import Client
from pyrogram.errors import (
    SessionPasswordNeeded, PhoneCodeInvalid, PhoneCodeExpired,
    PhoneNumberInvalid, FloodWait
)

logging.basicConfig(level=logging.INFO)
app = Flask(__name__)

# ── Config (set via env or edit here) ────────────────────────────────────────
API_ID   = int(os.environ.get("TG_API_ID",   "0"))   # from my.telegram.org
API_HASH = os.environ.get("TG_API_HASH",     "")
SESSION_DIR = os.environ.get("SESSION_DIR",  "./sessions")

os.makedirs(SESSION_DIR, exist_ok=True)

# ── In-memory store for pending verifications ─────────────────────────────────
# phone -> {"client": ..., "hash": ..., "phone_code_hash": ...}
pending: dict = {}


def get_client(phone: str) -> Client:
    safe_phone = phone.replace("+", "").replace(" ", "")
    return Client(
        f"{SESSION_DIR}/{safe_phone}",
        api_id=API_ID,
        api_hash=API_HASH,
    )


# ── POST /send_code ───────────────────────────────────────────────────────────
@app.post("/send_code")
def send_code():
    data  = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    if not phone:
        return jsonify(ok=False, error="phone required"), 400

    try:
        client = get_client(phone)
        client.connect()
        sent   = client.send_code(phone)
        pending[phone] = {"client": client, "phone_code_hash": sent.phone_code_hash}
        logging.info("Code sent to %s", phone)
        return jsonify(ok=True, phone_code_hash=sent.phone_code_hash)
    except PhoneNumberInvalid:
        return jsonify(ok=False, error="Invalid phone number")
    except FloodWait as e:
        return jsonify(ok=False, error=f"Flood wait {e.value}s")
    except Exception as e:
        logging.exception("send_code error")
        return jsonify(ok=False, error=str(e))


# ── POST /verify_code ─────────────────────────────────────────────────────────
@app.post("/verify_code")
def verify_code():
    data  = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    code  = data.get("code", "").strip()
    hash_ = data.get("phone_code_hash", "").strip()

    if not phone or not code:
        return jsonify(ok=False, error="phone and code required"), 400

    p = pending.get(phone)
    if p is None:
        return jsonify(ok=False, error="No pending session for this phone"), 400

    client: Client = p["client"]
    phone_code_hash = hash_ or p["phone_code_hash"]

    try:
        client.sign_in(phone, phone_code_hash, code)
        client.disconnect()
        del pending[phone]
        logging.info("Account verified: %s", phone)
        return jsonify(ok=True)
    except SessionPasswordNeeded:
        return jsonify(ok=False, needs_2fa=True)
    except (PhoneCodeInvalid, PhoneCodeExpired) as e:
        return jsonify(ok=False, error=f"Code error: {type(e).__name__}")
    except Exception as e:
        logging.exception("verify_code error")
        return jsonify(ok=False, error=str(e))


# ── POST /verify_2fa ──────────────────────────────────────────────────────────
@app.post("/verify_2fa")
def verify_2fa():
    data     = request.get_json(silent=True) or {}
    phone    = data.get("phone", "").strip()
    password = data.get("password", "")

    p = pending.get(phone)
    if p is None:
        return jsonify(ok=False, error="No pending session for this phone"), 400

    client: Client = p["client"]
    try:
        client.check_password(password)
        client.disconnect()
        del pending[phone]
        logging.info("2FA verified: %s", phone)
        return jsonify(ok=True)
    except Exception as e:
        logging.exception("verify_2fa error")
        return jsonify(ok=False, error=str(e))


# ── Health ────────────────────────────────────────────────────────────────────
@app.get("/health")
def health():
    return jsonify(ok=True, api_id_set=API_ID != 0)


if __name__ == "__main__":
    port = int(os.environ.get("PORT", 5050))
    logging.info("Pyrogram auth service starting on :%d", port)
    app.run(host="0.0.0.0", port=port, debug=False)
