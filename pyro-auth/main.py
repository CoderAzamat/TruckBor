"""
TruckBor — Pyrogram Auth & Automation Microservice
Run:  pip install -r requirements.txt && python main.py
Port: 5050 (configure TelegramAuth:ServiceUrl = http://localhost:5050)

Endpoints:
  POST /send_code         — Send SMS code
  POST /verify_code       — Verify SMS code
  POST /verify_2fa        — Verify 2FA password
  POST /join_groups       — Join multiple groups from user account
  POST /send_message      — Send message from user account
  POST /fix_spam          — Auto-fix spam via @SpamBot
  POST /scrape_group      — Read recent messages from a group
  POST /search_groups     — Search Telegram for logistics groups
  GET  /session/{phone}   — Get session string for a phone
  GET  /health            — Health check
"""

import os
import json
import time
import re
import logging
import asyncio
from threading import Thread
from flask import Flask, request, jsonify
from pyrogram import Client
from pyrogram.errors import (
    SessionPasswordNeeded, PhoneCodeInvalid, PhoneCodeExpired,
    PhoneNumberInvalid, FloodWait, UserAlreadyParticipant,
    InviteHashExpired, InviteHashInvalid, ChannelPrivate,
    ChatWriteForbidden, PeerFlood, UserBannedInChannel,
    SlowmodeWait
)
from pyrogram.types import Message
from pyrogram.enums import ChatType

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
app = Flask(__name__)

# ── Config ───────────────────────────────────────────────────────────────────
API_ID       = int(os.environ.get("TG_API_ID", "0"))
API_HASH     = os.environ.get("TG_API_HASH", "")
SESSION_DIR  = os.environ.get("SESSION_DIR", "./sessions")
BOT_AD_TEXT  = os.environ.get("BOT_AD_TEXT", "\n\n🤖 TruckBor Bot orqali yuborildi")

os.makedirs(SESSION_DIR, exist_ok=True)

# ── In-memory pending verifications ──────────────────────────────────────────
pending: dict = {}  # phone -> {"client": Client, "phone_code_hash": str}

# ── Async event loop in background thread ────────────────────────────────────
loop = asyncio.new_event_loop()
def _run_loop():
    asyncio.set_event_loop(loop)
    loop.run_forever()
Thread(target=_run_loop, daemon=True).start()

def run_async(coro):
    """Run async coroutine from sync Flask context."""
    future = asyncio.run_coroutine_threadsafe(coro, loop)
    return future.result(timeout=120)


def get_client(phone: str) -> Client:
    safe = phone.replace("+", "").replace(" ", "")
    return Client(
        name=f"{SESSION_DIR}/{safe}",
        api_id=API_ID,
        api_hash=API_HASH,
        in_memory=False,
    )


def get_session_path(phone: str) -> str:
    safe = phone.replace("+", "").replace(" ", "")
    return f"{SESSION_DIR}/{safe}.session"


# ═══════════════════════════════════════════════════════════════════════════════
#  AUTH ENDPOINTS
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/send_code")
def send_code():
    data = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    if not phone:
        return jsonify(ok=False, error="phone required"), 400

    try:
        client = get_client(phone)
        run_async(client.connect())
        sent = run_async(client.send_code(phone))
        pending[phone] = {"client": client, "phone_code_hash": sent.phone_code_hash}
        logging.info("Code sent to %s", phone)
        return jsonify(ok=True, phone_code_hash=sent.phone_code_hash)
    except PhoneNumberInvalid:
        return jsonify(ok=False, error="Noto'g'ri telefon raqam")
    except FloodWait as e:
        return jsonify(ok=False, error=f"Telegram cheklovi: {e.value} soniya kuting")
    except Exception as e:
        logging.exception("send_code error")
        return jsonify(ok=False, error=str(e))


@app.post("/verify_code")
def verify_code():
    data = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    code = data.get("code", "").strip()
    hash_ = data.get("phone_code_hash", "").strip()

    if not phone or not code:
        return jsonify(ok=False, error="phone and code required"), 400

    p = pending.get(phone)
    if p is None:
        return jsonify(ok=False, error="Sessiya topilmadi. Qaytadan urinib ko'ring."), 400

    client: Client = p["client"]
    phone_code_hash = hash_ or p["phone_code_hash"]

    try:
        user = run_async(client.sign_in(phone, phone_code_hash, code))
        # Get session string and user info
        session_string = run_async(client.export_session_string())
        user_info = _extract_user_info(user)

        run_async(client.disconnect())
        del pending[phone]

        logging.info("Account verified: %s (id=%s)", phone, user_info.get("id"))
        return jsonify(
            ok=True,
            session_string=session_string,
            user=user_info
        )
    except SessionPasswordNeeded:
        return jsonify(ok=False, needs_2fa=True)
    except (PhoneCodeInvalid, PhoneCodeExpired) as e:
        return jsonify(ok=False, error=f"Noto'g'ri yoki eskirgan kod")
    except Exception as e:
        logging.exception("verify_code error")
        return jsonify(ok=False, error=str(e))


@app.post("/verify_2fa")
def verify_2fa():
    data = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    password = data.get("password", "")

    p = pending.get(phone)
    if p is None:
        return jsonify(ok=False, error="Sessiya topilmadi"), 400

    client: Client = p["client"]
    try:
        user = run_async(client.check_password(password))
        session_string = run_async(client.export_session_string())
        user_info = _extract_user_info(user)

        run_async(client.disconnect())
        del pending[phone]

        logging.info("2FA verified: %s", phone)
        return jsonify(
            ok=True,
            session_string=session_string,
            user=user_info
        )
    except Exception as e:
        logging.exception("verify_2fa error")
        return jsonify(ok=False, error=str(e))


# ═══════════════════════════════════════════════════════════════════════════════
#  GROUP OPERATIONS
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/join_groups")
def join_groups():
    """
    Join multiple groups/channels from a user account.
    Body: { "phone": "+998...", "session_string": "...", "groups": ["@group1", "https://t.me/+hash", ...] }
    """
    data = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()
    session_string = data.get("session_string", "").strip()
    groups = data.get("groups", [])

    if not session_string or not groups:
        return jsonify(ok=False, error="session_string and groups required"), 400

    try:
        client = Client(
            name=f"{SESSION_DIR}/join_{phone.replace('+','').replace(' ','')}",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        results = []
        for group in groups:
            result = _join_one_group(client, group)
            results.append(result)
            time.sleep(2)  # delay between joins to avoid flood

        run_async(client.disconnect())

        joined = sum(1 for r in results if r["ok"])
        logging.info("Joined %d/%d groups for %s", joined, len(groups), phone)
        return jsonify(ok=True, joined=joined, total=len(groups), results=results)

    except FloodWait as e:
        return jsonify(ok=False, error=f"Telegram cheklovi: {e.value}s kuting")
    except Exception as e:
        logging.exception("join_groups error")
        return jsonify(ok=False, error=str(e))


def _join_one_group(client: Client, group_link: str) -> dict:
    """Join a single group, handle various error cases."""
    try:
        # Determine if it's an invite link or username
        if "t.me/+" in group_link or "joinchat/" in group_link:
            # Invite hash link
            hash_match = re.search(r"(?:t\.me/\+|joinchat/)([a-zA-Z0-9_-]+)", group_link)
            if hash_match:
                chat = run_async(client.join_chat(hash_match.group(1)))
            else:
                return {"group": group_link, "ok": False, "error": "Invalid invite link"}
        elif group_link.startswith("@"):
            chat = run_async(client.join_chat(group_link))
        elif "t.me/" in group_link:
            username = group_link.split("t.me/")[-1].split("/")[0].split("?")[0]
            chat = run_async(client.join_chat(username))
        else:
            chat = run_async(client.join_chat(group_link))

        return {
            "group": group_link,
            "ok": True,
            "chat_id": chat.id,
            "title": chat.title or ""
        }
    except UserAlreadyParticipant:
        return {"group": group_link, "ok": True, "already_member": True}
    except (InviteHashExpired, InviteHashInvalid):
        return {"group": group_link, "ok": False, "error": "Havola eskirgan/noto'g'ri"}
    except ChannelPrivate:
        return {"group": group_link, "ok": False, "error": "Yopiq guruh"}
    except FloodWait as e:
        return {"group": group_link, "ok": False, "error": f"FloodWait {e.value}s"}
    except Exception as e:
        return {"group": group_link, "ok": False, "error": str(e)[:100]}


# ═══════════════════════════════════════════════════════════════════════════════
#  MESSAGE SENDING (from user accounts)
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/send_message")
def send_message():
    """
    Send a message from a user account to a chat.
    Body: {
      "session_string": "...",
      "chat_id": -1001234567890,   // or "@username"
      "text": "Message text",
      "add_bot_ad": true           // append bot advertising text
    }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()
    chat_id = data.get("chat_id")
    text = data.get("text", "").strip()
    add_bot_ad = data.get("add_bot_ad", True)

    if not session_string or not chat_id or not text:
        return jsonify(ok=False, error="session_string, chat_id, text required"), 400

    if add_bot_ad:
        text += BOT_AD_TEXT

    try:
        client = Client(
            name="send_tmp",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        msg = run_async(client.send_message(chat_id, text))

        run_async(client.disconnect())

        return jsonify(ok=True, message_id=msg.id)

    except PeerFlood:
        return jsonify(ok=False, error="SPAM", is_spam=True)
    except UserBannedInChannel:
        return jsonify(ok=False, error="BANNED", is_banned=True)
    except ChatWriteForbidden:
        return jsonify(ok=False, error="Yozish taqiqlangan")
    except SlowmodeWait as e:
        return jsonify(ok=False, error=f"Slowmode: {e.value}s kuting")
    except FloodWait as e:
        return jsonify(ok=False, error=f"FloodWait: {e.value}s", flood_wait=e.value)
    except Exception as e:
        logging.exception("send_message error")
        return jsonify(ok=False, error=str(e)[:200])


@app.post("/send_to_groups")
def send_to_groups():
    """
    Send a message to multiple groups from a user account.
    Body: {
      "session_string": "...",
      "groups": [-1001234, -1005678, ...],
      "text": "Message text",
      "delay_seconds": 3,
      "add_bot_ad": true
    }
    Returns: { "ok": true, "sent": 45, "failed": 5, "spam": false, "results": [...] }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()
    groups = data.get("groups", [])
    text = data.get("text", "").strip()
    delay = data.get("delay_seconds", 3)
    add_bot_ad = data.get("add_bot_ad", True)

    if not session_string or not groups or not text:
        return jsonify(ok=False, error="session_string, groups, text required"), 400

    if add_bot_ad:
        text += BOT_AD_TEXT

    try:
        client = Client(
            name="batch_send",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        results = []
        spam_detected = False

        for gid in groups:
            try:
                run_async(client.send_message(gid, text))
                results.append({"group": gid, "ok": True})
            except PeerFlood:
                spam_detected = True
                results.append({"group": gid, "ok": False, "error": "SPAM"})
                logging.warning("SPAM detected for session, stopping batch")
                break  # stop immediately on spam
            except FloodWait as e:
                if e.value > 60:
                    results.append({"group": gid, "ok": False, "error": f"FloodWait {e.value}s"})
                    break
                time.sleep(e.value + 1)
                try:
                    run_async(client.send_message(gid, text))
                    results.append({"group": gid, "ok": True})
                except Exception as e2:
                    results.append({"group": gid, "ok": False, "error": str(e2)[:100]})
            except (UserBannedInChannel, ChatWriteForbidden):
                results.append({"group": gid, "ok": False, "error": "banned/forbidden"})
            except Exception as e:
                results.append({"group": gid, "ok": False, "error": str(e)[:100]})

            if not spam_detected:
                time.sleep(delay)

        run_async(client.disconnect())

        sent = sum(1 for r in results if r["ok"])
        failed = len(results) - sent
        logging.info("Batch send: %d/%d sent, spam=%s", sent, len(groups), spam_detected)

        return jsonify(
            ok=True, sent=sent, failed=failed,
            total=len(groups), spam=spam_detected, results=results
        )
    except Exception as e:
        logging.exception("send_to_groups error")
        return jsonify(ok=False, error=str(e)[:200])


# ═══════════════════════════════════════════════════════════════════════════════
#  ANTI-SPAM
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/fix_spam")
def fix_spam():
    """
    Auto-fix spam by sending /start to @SpamBot 3 times.
    Body: { "session_string": "..." }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()

    if not session_string:
        return jsonify(ok=False, error="session_string required"), 400

    try:
        client = Client(
            name="antispam",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        # Check if user has premium
        me = run_async(client.get_me())
        is_premium = getattr(me, 'is_premium', False)

        # Send /start to @SpamBot 3 times
        for i in range(3):
            try:
                run_async(client.send_message("SpamBot", "/start"))
                time.sleep(2)
            except Exception as e:
                logging.warning("SpamBot attempt %d failed: %s", i+1, e)

        run_async(client.disconnect())

        return jsonify(
            ok=True,
            is_premium=is_premium,
            message="@SpamBot ga 3 marta /start yuborildi"
        )
    except Exception as e:
        logging.exception("fix_spam error")
        return jsonify(ok=False, error=str(e)[:200])


# ═══════════════════════════════════════════════════════════════════════════════
#  SCRAPING — Read messages from groups
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/scrape_group")
def scrape_group():
    """
    Read recent messages from a group.
    Body: { "session_string": "...", "chat_id": -1001234, "limit": 50 }
    Returns: { "ok": true, "messages": [...] }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()
    chat_id = data.get("chat_id")
    limit = min(data.get("limit", 50), 200)

    if not session_string or not chat_id:
        return jsonify(ok=False, error="session_string and chat_id required"), 400

    try:
        client = Client(
            name="scrape",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        messages = []
        async def _scrape():
            async for msg in client.get_chat_history(chat_id, limit=limit):
                if msg.text:
                    messages.append({
                        "id": msg.id,
                        "text": msg.text[:2000],
                        "date": msg.date.isoformat() if msg.date else None,
                        "from_id": msg.from_user.id if msg.from_user else None,
                        "from_name": (msg.from_user.first_name or "") if msg.from_user else None,
                    })

        run_async(_scrape())
        run_async(client.disconnect())

        logging.info("Scraped %d messages from %s", len(messages), chat_id)
        return jsonify(ok=True, messages=messages)

    except Exception as e:
        logging.exception("scrape_group error")
        return jsonify(ok=False, error=str(e)[:200])


@app.post("/search_groups")
def search_groups():
    """
    Search Telegram for public groups by keyword.
    Body: { "session_string": "...", "query": "logistika yuk", "limit": 30 }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()
    query = data.get("query", "").strip()
    limit = min(data.get("limit", 30), 100)

    if not session_string or not query:
        return jsonify(ok=False, error="session_string and query required"), 400

    try:
        client = Client(
            name="search",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())

        async def _search():
            results = []
            try:
                found = await client.search_global(query, limit=limit)
                seen_chats = set()
                for msg in found:
                    chat = msg.chat
                    if chat and chat.id not in seen_chats and chat.type in (ChatType.GROUP, ChatType.SUPERGROUP, ChatType.CHANNEL):
                        seen_chats.add(chat.id)
                        results.append({
                            "chat_id": chat.id,
                            "title": chat.title or "",
                            "username": chat.username or "",
                            "members_count": getattr(chat, 'members_count', 0) or 0,
                            "type": str(chat.type),
                        })
            except Exception as e:
                logging.warning("search_global error: %s", e)
            return results

        groups_found = run_async(_search())
        run_async(client.disconnect())

        logging.info("Found %d groups for query '%s'", len(groups_found), query)
        return jsonify(ok=True, groups=groups_found)

    except Exception as e:
        logging.exception("search_groups error")
        return jsonify(ok=False, error=str(e)[:200])


# ═══════════════════════════════════════════════════════════════════════════════
#  SESSION MANAGEMENT
# ═══════════════════════════════════════════════════════════════════════════════

@app.post("/get_session_string")
def get_session_string():
    """
    Connect with existing session file and export session string.
    Body: { "phone": "+998..." }
    """
    data = request.get_json(silent=True) or {}
    phone = data.get("phone", "").strip()

    if not phone:
        return jsonify(ok=False, error="phone required"), 400

    try:
        client = get_client(phone)
        run_async(client.connect())

        if not run_async(client.get_me()):
            run_async(client.disconnect())
            return jsonify(ok=False, error="Session not authorized")

        session_string = run_async(client.export_session_string())
        me = run_async(client.get_me())
        user_info = _extract_user_info(me)

        run_async(client.disconnect())
        return jsonify(ok=True, session_string=session_string, user=user_info)

    except Exception as e:
        logging.exception("get_session_string error")
        return jsonify(ok=False, error=str(e)[:200])


@app.post("/check_session")
def check_session():
    """
    Check if a session string is still valid.
    Body: { "session_string": "..." }
    """
    data = request.get_json(silent=True) or {}
    session_string = data.get("session_string", "").strip()

    if not session_string:
        return jsonify(ok=False, error="session_string required"), 400

    try:
        client = Client(
            name="check",
            api_id=API_ID, api_hash=API_HASH,
            session_string=session_string,
            in_memory=True
        )
        run_async(client.connect())
        me = run_async(client.get_me())
        user_info = _extract_user_info(me)
        is_premium = getattr(me, 'is_premium', False)
        run_async(client.disconnect())

        return jsonify(ok=True, valid=True, is_premium=is_premium, user=user_info)
    except Exception as e:
        return jsonify(ok=True, valid=False, error=str(e)[:200])


# ═══════════════════════════════════════════════════════════════════════════════
#  HELPERS
# ═══════════════════════════════════════════════════════════════════════════════

def _extract_user_info(user) -> dict:
    if user is None:
        return {}
    return {
        "id": getattr(user, 'id', 0),
        "first_name": getattr(user, 'first_name', '') or '',
        "last_name": getattr(user, 'last_name', '') or '',
        "username": getattr(user, 'username', '') or '',
        "phone": getattr(user, 'phone_number', '') or '',
        "is_premium": getattr(user, 'is_premium', False) or False,
    }


@app.get("/health")
def health():
    return jsonify(
        ok=True,
        api_id_set=API_ID != 0,
        sessions_dir=SESSION_DIR,
        pending_auth=len(pending)
    )


if __name__ == "__main__":
    port = int(os.environ.get("PORT", 5050))
    logging.info("TruckBor Pyrogram service starting on :%d (API_ID=%s)", port, API_ID)
    app.run(host="0.0.0.0", port=port, debug=False)
