using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Telegram.Keyboards;

public static class Keyboards
{
    // ═══ LANGUAGE ═══════════════════════════
    public static InlineKeyboardMarkup LanguageMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🇺🇿 O'zbekcha (lotin)",   "lang:uz_latin") },
        new[] { InlineKeyboardButton.WithCallbackData("🇺🇿 Ўзбекча (кирилл)",   "lang:uz_cyrillic") },
        new[] { InlineKeyboardButton.WithCallbackData("🇷🇺 Русский",            "lang:russian") },
        new[] { InlineKeyboardButton.WithCallbackData("🇬🇧 English",            "lang:english") },
        new[] { InlineKeyboardButton.WithCallbackData("🇹🇷 Türkçe",             "lang:turkish") }
    });

    // ═══ ROLE ════════════════════════════════
    public static InlineKeyboardMarkup RoleMenu(Language lang) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData(
            lang == Language.Russian    ? "🚛 Водитель / Перевозчик" :
            lang == Language.English    ? "🚛 Driver / Carrier" :
            lang == Language.Turkish    ? "🚛 Sürücü / Taşıyıcı" :
            lang == Language.UzCyrillic ? "🚛 Ҳайдовчи / Юк ташувчи" :
            "🚛 Haydovchi / Yuk tashuvchi", "role:driver") },
        new[] { InlineKeyboardButton.WithCallbackData(
            lang == Language.Russian    ? "📦 Владелец груза / Отправитель" :
            lang == Language.English    ? "📦 Cargo owner / Sender" :
            lang == Language.Turkish    ? "📦 Yük sahibi / Gönderici" :
            lang == Language.UzCyrillic ? "📦 Юк эгаси / Юборувчи" :
            "📦 Yuk egasi / Yuboruvchi", "role:cargo_owner") },
        new[] { InlineKeyboardButton.WithCallbackData(
            lang == Language.Russian    ? "🧭 Логист / Диспетчер" :
            lang == Language.English    ? "🧭 Logist / Dispatcher" :
            lang == Language.Turkish    ? "🧭 Lojistikçi / Organizatör" :
            lang == Language.UzCyrillic ? "🧭 Логист / Диспетчер" :
            "🧭 Logist / Dispetcher", "role:logist") }
    });

    // ═══ MAIN MENU ═══════════════════════════
    public static ReplyKeyboardMarkup MainMenu(Language lang) => new(new[]
    {
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "📦 Груз — объявление" :
            lang == Language.English    ? "📦 Cargo ad" :
            lang == Language.Turkish    ? "📦 Yük ilanı" :
            lang == Language.UzCyrillic ? "📦 Юк эълони" :
            "📦 Yuk e'loni",

            lang == Language.Russian    ? "🚛 Транспорт — объявление" :
            lang == Language.English    ? "🚛 Transport ad" :
            lang == Language.Turkish    ? "🚛 Taşıt ilanı" :
            lang == Language.UzCyrillic ? "🚛 Транспорт эълони" :
            "🚛 Transport e'loni"
        },
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "📮 Догруз — попутка" :
            lang == Language.English    ? "📮 Extra cargo" :
            lang == Language.Turkish    ? "📮 Yol arkadaşı" :
            lang == Language.UzCyrillic ? "📮 Доғруз (қўш юк)" :
            "📮 Dogruz (qo'sh yuk)",

            lang == Language.Russian    ? "🎯 Подходящие объявления" :
            lang == Language.English    ? "🎯 Matching ads" :
            lang == Language.Turkish    ? "🎯 Uygun ilanlar" :
            lang == Language.UzCyrillic ? "🎯 Мос эълонлар" :
            "🎯 Mos e'lonlar"
        },
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "🔍 Расширенный поиск" :
            lang == Language.English    ? "🔍 Advanced search" :
            lang == Language.Turkish    ? "🔍 Gelişmiş arama" :
            lang == Language.UzCyrillic ? "🔍 Кенгайтирилган қидируув" :
            "🔍 Kengaytirilgan qidiruv",

            lang == Language.Russian    ? "💎 VIP сервисы" :
            lang == Language.English    ? "💎 VIP services" :
            lang == Language.Turkish    ? "💎 VIP hizmetler" :
            lang == Language.UzCyrillic ? "💎 VIP Хизматлар" :
            "💎 VIP Xizmatlar"
        },
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "📊 Мой кабинет" :
            lang == Language.English    ? "📊 My cabinet" :
            lang == Language.Turkish    ? "📊 Kabinetim" :
            lang == Language.UzCyrillic ? "📊 Менинг кабинетим" :
            "📊 Mening kabinetim",

            lang == Language.Russian    ? "📱 Мои аккаунты" :
            lang == Language.English    ? "📱 My accounts" :
            lang == Language.Turkish    ? "📱 Hesaplarım" :
            lang == Language.UzCyrillic ? "📱 Аккаунтларим" :
            "📱 Akkauntlarim"
        },
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "⚙️ Настройки" :
            lang == Language.English    ? "⚙️ Settings" :
            lang == Language.Turkish    ? "⚙️ Ayarlar" :
            lang == Language.UzCyrillic ? "⚙️ Созламалар" :
            "⚙️ Sozlamalar",

            lang == Language.Russian    ? "ℹ️ Помощь" :
            lang == Language.English    ? "ℹ️ Help" :
            lang == Language.Turkish    ? "ℹ️ Yardım" :
            lang == Language.UzCyrillic ? "ℹ️ Ёрдам" :
            "ℹ️ Yordam"
        }
    })
    { ResizeKeyboard = true };

    // ═══ ADMIN MENU ══════════════════════════
    public static ReplyKeyboardMarkup AdminMenu() => new(new[]
    {
        new KeyboardButton[] { "📊 Statistika",    "👥 Foydalanuvchilar" },
        new KeyboardButton[] { "💰 To'lovlar",     "⭐ Tariflar" },
        new KeyboardButton[] { "📢 Kanallar",      "💳 Kartalar" },
        new KeyboardButton[] { "📱 Akkauntlar",    "🌐 Guruhlar" },
        new KeyboardButton[] { "📬 Broadcast",     "⚙️ Sozlamalar" },
        new KeyboardButton[] { "🔒 Maj.obuna",     "🗑 Tozalash" },
        new KeyboardButton[] { "👑 Adminlar",      "📹 Videolar" },
        new KeyboardButton[] { "💎 Premium",       "📞 Virtual" },
        new KeyboardButton[] { "🌐 Web panel",     "📊 Analitika" },
        new KeyboardButton[] { "🏠 Asosiy menyu" }
    })
    { ResizeKeyboard = true };

    // ═══ REQUEST CONTACT ═════════════════════
    public static ReplyKeyboardMarkup RequestContact(Language lang) => new(new[]
    {
        new[]
        {
            new KeyboardButton(
                lang == Language.Russian    ? "📱 Отправить номер" :
                lang == Language.English    ? "📱 Send number" :
                lang == Language.Turkish    ? "📱 Numara gönder" :
                lang == Language.UzCyrillic ? "📱 Рақамни юбориш" :
                "📱 Raqamni yuborish")
            { RequestContact = true }
        },
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "❌ Отмена" :
            lang == Language.English    ? "❌ Cancel" :
            lang == Language.Turkish    ? "❌ İptal" :
            lang == Language.UzCyrillic ? "❌ Бекор қилиш" :
            "❌ Bekor qilish"
        }
    })
    { ResizeKeyboard = true, OneTimeKeyboard = true };

    // ═══ CANCEL ══════════════════════════════
    public static ReplyKeyboardMarkup CancelMenu(Language lang) => new(new[]
    {
        new KeyboardButton[]
        {
            lang == Language.Russian    ? "❌ Отмена" :
            lang == Language.English    ? "❌ Cancel" :
            lang == Language.Turkish    ? "❌ İptal" :
            lang == Language.UzCyrillic ? "❌ Бекор қилиш" :
            "❌ Bekor qilish"
        }
    })
    { ResizeKeyboard = true };

    // ═══ VEHICLE TYPE ════════════════════════
    public static ReplyKeyboardMarkup VehicleTypeMenu(Language lang)
    {
        var cancel = lang == Language.Russian ? "❌ Отмена" :
                     lang == Language.English ? "❌ Cancel" :
                     lang == Language.Turkish ? "❌ İptal" :
                     lang == Language.UzCyrillic ? "❌ Бекор қилиш" :
                     "❌ Bekor qilish";
        return new(new[]
        {
            new KeyboardButton[] { "🚛 Tent/Fura (20t)",    "🧊 Izotermal" },
            new KeyboardButton[] { "❄️ Ref/Sovutgich",      "📦 Chakman/Bort" },
            new KeyboardButton[] { "🚐 Sprinter",           "🚚 Isuzu/Gazel" },
            new KeyboardButton[] { "🔩 KAMAZ (10-20t)",     "⚓ Tral (yirik)" },
            new KeyboardButton[] { "🛻 Labo/Pickup",        "🏍 Mototsikl" },
            new KeyboardButton[] { cancel }
        })
        { ResizeKeyboard = true };
    }

    // ═══ TARIFFS ═════════════════════════════
    public static InlineKeyboardMarkup TariffList(
        IEnumerable<(long id, string name, decimal price, bool recommended)> tariffs)
    {
        var rows = tariffs.Select(t =>
        {
            var star = t.recommended ? "🔥" : "⭐";
            return new[] { InlineKeyboardButton.WithCallbackData(
                $"{star} {t.name} — {t.price:N0} so'm/oy",
                $"tariff:buy:{t.id}") };
        }).ToList();
        return new InlineKeyboardMarkup(rows);
    }

    // ═══ PAYMENT ═════════════════════════════
    public static InlineKeyboardMarkup PaymentConfirmation(long paymentId) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Tasdiqlash", $"pay:approve:{paymentId}"),
            InlineKeyboardButton.WithCallbackData("❌ Rad etish",  $"pay:reject:{paymentId}")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("👤 Foydalanuvchi", $"pay:viewuser:{paymentId}")
        }
    });

    // ═══ CHANNEL SUBSCRIPTION ════════════════
    public static InlineKeyboardMarkup ChannelSubscription(
        IEnumerable<(string title, string link)> channels, Language lang)
    {
        var rows = channels
            .Select(c => new[] { InlineKeyboardButton.WithUrl($"📢 {c.title}", c.link) })
            .ToList();
        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian    ? "✅ Я подписался — проверить" :
                lang == Language.English    ? "✅ Subscribed — check" :
                lang == Language.Turkish    ? "✅ Abone oldum — kontrol et" :
                lang == Language.UzCyrillic ? "✅ Аъзо бўлдим — текшириш" :
                "✅ A'zo bo'ldim — tekshirish", "check:subscription")
        });
        return new InlineKeyboardMarkup(rows);
    }

    // ═══ USER ACTIONS (ADMIN) ═════════════════
    public static InlineKeyboardMarkup UserActions(long userId, bool isBanned) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("💰 Balans",       $"user:balance:{userId}"),
            InlineKeyboardButton.WithCallbackData("✉️ Xabar",        $"user:msg:{userId}")
        },
        new[]
        {
            isBanned
                ? InlineKeyboardButton.WithCallbackData("🔓 Blokdan chiqar", $"user:unban:{userId}")
                : InlineKeyboardButton.WithCallbackData("🚫 Bloklash",       $"user:ban:{userId}"),
            InlineKeyboardButton.WithCallbackData("⭐ Tarif ber", $"user:givetariff:{userId}")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("💎 Premium ber",  $"user:givepremium:{userId}"),
            InlineKeyboardButton.WithCallbackData("🗑 O'chirish",    $"user:delete:{userId}")
        }
    });

    // ═══ CONFIRM ════════════════════════════
    public static InlineKeyboardMarkup Confirm(string yesData, string yesLabel = "✅ Ha") => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(yesLabel,     yesData),
            InlineKeyboardButton.WithCallbackData("❌ Yo'q",   "menu:cancel")
        }
    });

    // ═══ BACK ════════════════════════════════
    public static InlineKeyboardMarkup BackMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Orqaga", "menu:main") }
    });

    // ═══ SETTINGS ════════════════════════════
    public static InlineKeyboardMarkup SettingsMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📞 Support username",        "settings:support_username") },
        new[] { InlineKeyboardButton.WithCallbackData("📢 Kanal username",          "settings:channel_username") },
        new[] { InlineKeyboardButton.WithCallbackData("🔧 Texnik rejim (true/false)", "settings:maintenance_mode") },
        new[] { InlineKeyboardButton.WithCallbackData("📢 Post kanali ID",          "settings:post_channel_id") },
        new[] { InlineKeyboardButton.WithCallbackData("⏱ Post interval (daqiqa)", "settings:auto_post_interval_minutes") }
    });

    // ═══ CLEAR DATA ══════════════════════════
    public static InlineKeyboardMarkup ClearDataMenu() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Foydalanuvchilarni tozalash", "clear:users") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 To'lovlarni tozalash",       "clear:payments") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Obunalarni tozalash",        "clear:subs") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 E'lonlarni tozalash",        "clear:posts") },
        new[] { InlineKeyboardButton.WithCallbackData("⚠️ HAMMASINI TOZALASH",        "clear:all") },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Bekor",                      "menu:cancel") }
    });

    // ═══ MANDATORY MENU ══════════════════════
    public static InlineKeyboardMarkup MandatoryMenu(bool enabled) => new(new[]
    {
        new[]
        {
            enabled
                ? InlineKeyboardButton.WithCallbackData("🔴 O'chirish", "mandatory:off")
                : InlineKeyboardButton.WithCallbackData("🟢 Yoqish",    "mandatory:on")
        },
        new[] { InlineKeyboardButton.WithCallbackData("➕ Kanal qo'shish", "mandatory:add") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Tozalash",       "mandatory:clear") }
    });

    // ═══ GIVE TARIFF LIST ════════════════════
    public static InlineKeyboardMarkup GiveTariffList(
        long userId, IEnumerable<(long id, string name, int days)> tariffs)
    {
        var rows = tariffs.Select(t =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"⭐ {t.name} ({t.days} kun)",
                $"user:givetariff_confirm:{userId}:{t.id}") }).ToList();
        return new InlineKeyboardMarkup(rows);
    }

    // ═══ MY CABINET ══════════════════════════
    public static InlineKeyboardMarkup MyCabinetMenu(Language lang) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "💰 Баланс" : "💰 Balans", "cabinet:balance"),
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "📋 История платежей" : "📋 To'lovlar tarixi", "cabinet:payments")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "📦 Мои объявления" : "📦 Mening e'lonlarim", "cabinet:posts"),
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "⭐ Мой тариф" : "⭐ Mening tarifim", "cabinet:tariff")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "🌐 Открыть Mini App" : "🌐 Mini App ochish",
                "cabinet:miniapp")
        }
    });

    // ═══ SHOW PHONE (premium gating) ═════════
    public static InlineKeyboardMarkup ShowPhoneButton(long postId, Language lang) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                lang == Language.Russian ? "📞 Показать номер" :
                lang == Language.English ? "📞 Show number" :
                lang == Language.Turkish ? "📞 Numarayı göster" :
                lang == Language.UzCyrillic ? "📞 Рақамни кўрсатиш" :
                "📞 Raqamni ko'rish",
                $"post:phone:{postId}")
        }
    });

    // ═══ MATCHING ADS ════════════════════════
    public static InlineKeyboardMarkup MatchingAdActions(long postId) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("📞 Aloqa", $"post:phone:{postId}"),
            InlineKeyboardButton.WithCallbackData("🔄 Ko'proq", "match:more")
        }
    });

    // ═══ WELCOME COMPLETE / QUICK ACTIONS ════
    public static InlineKeyboardMarkup WelcomeCompleteMenu(Language lang, long userId, bool isAdmin)
    {
        string L(string uz, string uzc, string ru, string en, string tr) => lang switch
        {
            Language.UzCyrillic => uzc,
            Language.Russian    => ru,
            Language.English    => en,
            Language.Turkish    => tr,
            _                   => uz
        };

        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithWebApp(
                    L("📱 Mini App ochish", "📱 Mini App очиш", "📱 Открыть Mini App", "📱 Open Mini App", "📱 Mini App'ı Aç"),
                    new WebAppInfo { Url = "https://t.me/TruckBorBot/app" })
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    L("💎 VIP Obuna", "💎 VIP Обуна", "💎 VIP Подписка", "💎 VIP Subscription", "💎 VIP Abonelik"),
                    "quick:vip"),
                InlineKeyboardButton.WithCallbackData(
                    L("💰 Balans", "💰 Баланс", "💰 Баланс", "💰 Balance", "💰 Bakiye"),
                    "quick:balance")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    L("📞 Virtual Raqam", "📞 Виртуал рақам", "📞 Виртуальный номер", "📞 Virtual Number", "📞 Sanal Numara"),
                    "quick:vnumbers"),
                InlineKeyboardButton.WithCallbackData(
                    L("💳 TG Premium", "💳 TG Premium", "💳 TG Premium", "💳 TG Premium", "💳 TG Premium"),
                    "quick:premium")
            },
            new[]
            {
                InlineKeyboardButton.WithUrl(
                    L("📞 Yordam", "📞 Ёрдам", "📞 Поддержка", "📞 Support", "📞 Destek"),
                    "https://t.me/TruckBorAdmin"),
                InlineKeyboardButton.WithUrl(
                    L("📢 Kanal", "📢 Канал", "📢 Канал", "📢 Channel", "📢 Kanal"),
                    "https://t.me/TruckBorUz")
            }
        };

        if (isAdmin)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("👨‍💼 Admin paneli", "quick:adminpanel")
            });
        }

        return new InlineKeyboardMarkup(rows);
    }

    // ═══ BALANCE MENU ═══════════════════════
    public static InlineKeyboardMarkup BalanceMenu(Language lang)
    {
        string L(string uz, string ru, string en) => lang == Language.Russian ? ru : lang == Language.English ? en : uz;
        return new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(L("💳 Balans to'ldirish", "💳 Пополнить баланс", "💳 Top up balance"), "balance:topup") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(L("📋 To'lovlar tarixi", "📋 История платежей", "📋 Payment history"), "cabinet:payments"),
                InlineKeyboardButton.WithCallbackData(L("⭐ VIP tariflar", "⭐ VIP тарифы", "⭐ VIP plans"), "quick:vip")
            }
        });
    }

    // ═══ PREMIUM MENU ═══════════════════════
    public static InlineKeyboardMarkup PremiumMenu(Language lang) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("💎 1 oy — 99 000 so'm (admin)", "premium:1month") },
        new[] { InlineKeyboardButton.WithCallbackData("⭐ Telegram Stars bilan to'lash", "premium:stars") },
        new[] { InlineKeyboardButton.WithCallbackData(lang == Language.Russian ? "🔙 Назад" : "🔙 Orqaga", "menu:cancel") }
    });

    // ═══ VIRTUAL NUMBERS MENU ════════════════
    public static InlineKeyboardMarkup VirtualNumbersMenu(Language lang) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🇺🇿 O'zbekiston — 2 000 so'm", "vnumber:buy:uz"), InlineKeyboardButton.WithCallbackData("🇷🇺 Rossiya — 1 500 so'm", "vnumber:buy:ru") },
        new[] { InlineKeyboardButton.WithCallbackData("🇰🇿 Qozog'iston — 1 800 so'm", "vnumber:buy:kz"), InlineKeyboardButton.WithCallbackData("🇬🇧 UK — 3 000 so'm", "vnumber:buy:uk") },
        new[] { InlineKeyboardButton.WithCallbackData("🇮🇳 Hindiston — 800 so'm", "vnumber:buy:in"), InlineKeyboardButton.WithCallbackData("🇵🇱 Polsha — 2 500 so'm", "vnumber:buy:pl") },
        new[] { InlineKeyboardButton.WithCallbackData("📋 Mening raqamlarim", "vnumber:list"), InlineKeyboardButton.WithCallbackData("💳 Balansni to'ldirish", "balance:topup") },
        new[] { InlineKeyboardButton.WithCallbackData(lang == Language.Russian ? "🔙 Назад" : "🔙 Orqaga", "menu:cancel") }
    });

}
