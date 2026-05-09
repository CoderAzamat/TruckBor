using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Services;

public class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, Dictionary<Language, string>> _strings = new()
    {
        // ── Core ─────────────────────────────────────────────────────────────

        ["welcome"] = new()
        {
            [Language.UzLatin]    = "🚛 <b>TruckBor</b> ga xush kelibsiz!\n\nO'zbekistonning №1 logistika platformasi!",
            [Language.UzCyrillic] = "🚛 <b>TruckBor</b>га хуш келибсиз!\n\nЎзбекистоннинг №1 логистика платформаси!",
            [Language.Russian]    = "🚛 Добро пожаловать в <b>TruckBor</b>!\n\nПлатформа логистики №1 в Узбекистане!",
            [Language.English]    = "🚛 Welcome to <b>TruckBor</b>!\n\nUzbekistan's #1 logistics platform!",
            [Language.Turkish]    = "🚛 <b>TruckBor</b>'a hoş geldiniz!\n\nÖzbekistan'ın 1 numaralı lojistik platformu!",
        },
        ["choose_language"] = new()
        {
            [Language.UzLatin]    = "🌐 Tilni tanlang:",
            [Language.UzCyrillic] = "🌐 Тилни танланг:",
            [Language.Russian]    = "🌐 Выберите язык:",
            [Language.English]    = "🌐 Choose language:",
            [Language.Turkish]    = "🌐 Dil seçin:",
        },
        ["main_menu"] = new()
        {
            [Language.UzLatin]    = "🏠 <b>Asosiy menyu</b>",
            [Language.UzCyrillic] = "🏠 <b>Асосий меню</b>",
            [Language.Russian]    = "🏠 <b>Главное меню</b>",
            [Language.English]    = "🏠 <b>Main menu</b>",
            [Language.Turkish]    = "🏠 <b>Ana menü</b>",
        },
        ["maintenance"] = new()
        {
            [Language.UzLatin]    = "🔧 Tizim yangilanmoqda. Tez orada qaytamiz!",
            [Language.UzCyrillic] = "🔧 Тизим янгиланмоқда. Тез орада қайтамиз!",
            [Language.Russian]    = "🔧 Система обновляется. Скоро вернёмся!",
            [Language.English]    = "🔧 System is updating. We'll be back soon!",
            [Language.Turkish]    = "🔧 Sistem güncelleniyor. Yakında döneceğiz!",
        },
        ["blocked"] = new()
        {
            [Language.UzLatin]    = "🚫 Hisobingiz bloklangan. Admin: @TruckBorAdmin",
            [Language.UzCyrillic] = "🚫 Ҳисобингиз блокланган. Админ: @TruckBorAdmin",
            [Language.Russian]    = "🚫 Ваш аккаунт заблокирован. Админ: @TruckBorAdmin",
            [Language.English]    = "🚫 Your account is blocked. Admin: @TruckBorAdmin",
            [Language.Turkish]    = "🚫 Hesabınız engellendi. Admin: @TruckBorAdmin",
        },
        ["flood_warning"] = new()
        {
            [Language.UzLatin]    = "⚠️ Juda tez bosyapsiz! Biroz kuting.",
            [Language.UzCyrillic] = "⚠️ Жуда тез босяпсиз! Бироз кутинг.",
            [Language.Russian]    = "⚠️ Вы нажимаете слишком быстро! Подождите.",
            [Language.English]    = "⚠️ You're pressing too fast! Please wait.",
            [Language.Turkish]    = "⚠️ Çok hızlı basıyorsunuz! Lütfen bekleyin.",
        },
        ["cancelled"] = new()
        {
            [Language.UzLatin]    = "❌ Bekor qilindi.",
            [Language.UzCyrillic] = "❌ Бекор қилинди.",
            [Language.Russian]    = "❌ Отменено.",
            [Language.English]    = "❌ Cancelled.",
            [Language.Turkish]    = "❌ İptal edildi.",
        },
        ["unknown_cmd"] = new()
        {
            [Language.UzLatin]    = "❓ Noma'lum buyruq. /start ni bosing.",
            [Language.UzCyrillic] = "❓ Номаълум буйруқ. /start ни босинг.",
            [Language.Russian]    = "❓ Неизвестная команда. Нажмите /start.",
            [Language.English]    = "❓ Unknown command. Press /start.",
            [Language.Turkish]    = "❓ Bilinmeyen komut. /start'a basın.",
        },
        ["home_greeting"] = new()
        {
            [Language.UzLatin]    = "🏠 <b>Asosiy menyu</b>\n\n👋 {0}!\n💰 Balans: <b>{1:N0}</b> so'm",
            [Language.UzCyrillic] = "🏠 <b>Асосий меню</b>\n\n👋 {0}!\n💰 Баланс: <b>{1:N0}</b> сўм",
            [Language.Russian]    = "🏠 <b>Главное меню</b>\n\n👋 {0}!\n💰 Баланс: <b>{1:N0}</b> сум",
            [Language.English]    = "🏠 <b>Main menu</b>\n\n👋 {0}!\n💰 Balance: <b>{1:N0}</b> UZS",
            [Language.Turkish]    = "🏠 <b>Ana menü</b>\n\n👋 {0}!\n💰 Bakiye: <b>{1:N0}</b> UZS",
        },
        ["language_name"] = new()
        {
            [Language.UzLatin]    = "O'zbekcha (lotin)",
            [Language.UzCyrillic] = "Ўзбекча (кирилл)",
            [Language.Russian]    = "Русский",
            [Language.English]    = "English",
            [Language.Turkish]    = "Türkçe",
        },

        // ── Onboarding — Step 1 (shown before language selected, keep inline) ─
        // Step 1 welcome is composed in the handler because lang is unknown.

        // ── Onboarding — Step 2: Role ─────────────────────────────────────────
        ["onboard_step_role"] = new()
        {
            [Language.UzLatin]    = "👤 <b>2-qadam / 5</b>  ▰▰▱▱▱\n\nRolingizni tanlang:",
            [Language.UzCyrillic] = "👤 <b>2-қадам / 5</b>  ▰▰▱▱▱\n\nРолингизни танланг:",
            [Language.Russian]    = "👤 <b>Шаг 2 / 5</b>  ▰▰▱▱▱\n\nВыберите вашу роль:",
            [Language.English]    = "👤 <b>Step 2 / 5</b>  ▰▰▱▱▱\n\nChoose your role:",
            [Language.Turkish]    = "👤 <b>Adım 2 / 5</b>  ▰▰▱▱▱\n\nRolünüzü seçin:",
        },

        // ── Onboarding — Step 3: Terms of Service ─────────────────────────────
        ["onboard_step_terms"] = new()
        {
            [Language.UzLatin] =
                "📋 <b>3-qadam / 5</b>  ▰▰▰▱▱\n\n" +
                "<b>Foydalanish shartlari</b>\n\n" +
                "1️⃣ Platformadan faqat qonuniy maqsadlarda foydalaning\n" +
                "2️⃣ Boshqa foydalanuvchilarga hurmat bilan munosabatda bo'ling\n" +
                "3️⃣ To'g'ri va aniq ma'lumot kiriting\n" +
                "4️⃣ Spam va noto'g'ri e'lonlar qat'iyan taqiqlangan\n" +
                "5️⃣ Adminlar qarorlariga bo'ysunish majburiy\n\n" +
                "⚠️ Qoidalarni buzish hisobni bloklashga olib keladi.",
            [Language.UzCyrillic] =
                "📋 <b>3-қадам / 5</b>  ▰▰▰▱▱\n\n" +
                "<b>Фойдаланиш шартлари</b>\n\n" +
                "1️⃣ Платформадан фақат қонуний мақсадларда фойдаланинг\n" +
                "2️⃣ Бошқа фойдаланувчиларга ҳурмат билан муносабатда бўлинг\n" +
                "3️⃣ Тўғри ва аниқ маълумот киритинг\n" +
                "4️⃣ Спам ва нотўғри эълонлар қатъиян тақиқланган\n" +
                "5️⃣ Админлар қарорларига бўйсуниш мажбурий\n\n" +
                "⚠️ Қоидаларни бузиш ҳисобни блоклашга олиб келади.",
            [Language.Russian] =
                "📋 <b>Шаг 3 / 5</b>  ▰▰▰▱▱\n\n" +
                "<b>Пользовательское соглашение</b>\n\n" +
                "1️⃣ Используйте платформу только в законных целях\n" +
                "2️⃣ Уважайте других пользователей\n" +
                "3️⃣ Предоставляйте точную и достоверную информацию\n" +
                "4️⃣ Спам и ложные объявления строго запрещены\n" +
                "5️⃣ Решения администраторов являются окончательными\n\n" +
                "⚠️ Нарушение правил может привести к блокировке аккаунта.",
            [Language.English] =
                "📋 <b>Step 3 / 5</b>  ▰▰▰▱▱\n\n" +
                "<b>Terms of Service</b>\n\n" +
                "1️⃣ Use the platform for legal purposes only\n" +
                "2️⃣ Treat other users with respect\n" +
                "3️⃣ Provide accurate and truthful information\n" +
                "4️⃣ Spam and false ads are strictly prohibited\n" +
                "5️⃣ Admin decisions are final\n\n" +
                "⚠️ Violations may result in permanent account blocking.",
            [Language.Turkish] =
                "📋 <b>Adım 3 / 5</b>  ▰▰▰▱▱\n\n" +
                "<b>Kullanım Şartları</b>\n\n" +
                "1️⃣ Platformu yalnızca yasal amaçlar için kullanın\n" +
                "2️⃣ Diğer kullanıcılara saygılı davranın\n" +
                "3️⃣ Doğru ve güvenilir bilgi sağlayın\n" +
                "4️⃣ Spam ve sahte ilanlar kesinlikle yasaktır\n" +
                "5️⃣ Yönetici kararları kesindir\n\n" +
                "⚠️ Kuralların ihlali hesabın engellenmesine yol açabilir.",
        },
        ["terms_accept_btn"] = new()
        {
            [Language.UzLatin]    = "✅ Qabul qilaman",
            [Language.UzCyrillic] = "✅ Қабул қиламан",
            [Language.Russian]    = "✅ Принимаю",
            [Language.English]    = "✅ I accept",
            [Language.Turkish]    = "✅ Kabul ediyorum",
        },

        // ── Onboarding — Step 4: Full Name ────────────────────────────────────
        ["onboard_step_name"] = new()
        {
            [Language.UzLatin]    = "✍️ <b>4-qadam / 5</b>  ▰▰▰▰▱\n\n<b>To'liq ismingizni kiriting:</b>\n\n<i>Masalan: Azamat Raxmonqulov</i>",
            [Language.UzCyrillic] = "✍️ <b>4-қадам / 5</b>  ▰▰▰▰▱\n\n<b>Тўлиқ исмингизни киритинг:</b>\n\n<i>Масалан: Азамат Раҳмонқулов</i>",
            [Language.Russian]    = "✍️ <b>Шаг 4 / 5</b>  ▰▰▰▰▱\n\n<b>Введите ваше полное имя:</b>\n\n<i>Пример: Азамат Рахмонкулов</i>",
            [Language.English]    = "✍️ <b>Step 4 / 5</b>  ▰▰▰▰▱\n\n<b>Enter your full name:</b>\n\n<i>Example: Azamat Rakhmonqulov</i>",
            [Language.Turkish]    = "✍️ <b>Adım 4 / 5</b>  ▰▰▰▰▱\n\n<b>Tam adınızı girin:</b>\n\n<i>Örnek: Azamat Rakhmonkulov</i>",
        },
        ["onboard_name_error"] = new()
        {
            [Language.UzLatin]    = "⚠️ Ism juda qisqa! Kamida 3 ta harf bo'lishi kerak.\n\n✍️ <b>4-qadam / 5</b>  ▰▰▰▰▱\n\n<b>Ism va familiyangizni to'liq kiriting:</b>\n\n<i>Masalan: Azamat Raxmonqulov</i>",
            [Language.UzCyrillic] = "⚠️ Исм жуда қисқа! Камида 3 та ҳарф бўлиши керак.\n\n✍️ <b>4-қадам / 5</b>  ▰▰▰▰▱\n\n<b>Исм ва фамилиянгизни тўлиқ киритинг:</b>\n\n<i>Масалан: Азамат Раҳмонқулов</i>",
            [Language.Russian]    = "⚠️ Имя слишком короткое! Минимум 3 символа.\n\n✍️ <b>Шаг 4 / 5</b>  ▰▰▰▰▱\n\n<b>Введите ваше полное имя:</b>\n\n<i>Пример: Азамат Рахмонкулов</i>",
            [Language.English]    = "⚠️ Name is too short! Minimum 3 characters.\n\n✍️ <b>Step 4 / 5</b>  ▰▰▰▰▱\n\n<b>Enter your full name:</b>\n\n<i>Example: Azamat Rakhmonqulov</i>",
            [Language.Turkish]    = "⚠️ İsim çok kısa! En az 3 karakter olmalı.\n\n✍️ <b>Adım 4 / 5</b>  ▰▰▰▰▱\n\n<b>Tam adınızı girin:</b>\n\n<i>Örnek: Azamat Rakhmonkulov</i>",
        },

        // ── Onboarding — Step 5: Phone ────────────────────────────────────────
        ["onboard_step_phone"] = new()
        {
            [Language.UzLatin]    = "📱 <b>5-qadam / 5</b>  ▰▰▰▰▰\n\nTelefon raqamingizni yuboring:",
            [Language.UzCyrillic] = "📱 <b>5-қадам / 5</b>  ▰▰▰▰▰\n\nТелефон рақамингизни юборинг:",
            [Language.Russian]    = "📱 <b>Шаг 5 / 5</b>  ▰▰▰▰▰\n\nОтправьте ваш номер телефона:",
            [Language.English]    = "📱 <b>Step 5 / 5</b>  ▰▰▰▰▰\n\nSend your phone number:",
            [Language.Turkish]    = "📱 <b>Adım 5 / 5</b>  ▰▰▰▰▰\n\nTelefon numaranızı gönderin:",
        },
        // Legacy key kept for backward compat
        ["ask_phone"] = new()
        {
            [Language.UzLatin]    = "📱 Telefon raqamingizni yuboring:",
            [Language.UzCyrillic] = "📱 Телефон рақамингизни юборинг:",
            [Language.Russian]    = "📱 Отправьте ваш номер телефона:",
            [Language.English]    = "📱 Send your phone number:",
            [Language.Turkish]    = "📱 Telefon numaranızı gönderin:",
        },
        ["ask_fullname"] = new()
        {
            [Language.UzLatin]    = "✍️ Ism va familyangizni kiriting:",
            [Language.UzCyrillic] = "✍️ Исм ва фамилиянгизни киритинг:",
            [Language.Russian]    = "✍️ Введите ваше имя и фамилию:",
            [Language.English]    = "✍️ Enter your full name:",
            [Language.Turkish]    = "✍️ Adınızı ve soyadınızı girin:",
        },

        // ── Onboarding — Completion ────────────────────────────────────────────
        // {0} = fullName, {1} = phone
        ["onboard_complete"] = new()
        {
            [Language.UzLatin] =
                "🎉 <b>Xush kelibsiz, {0}!</b>\n\n" +
                "━━━━━━━━━━━━━━━━━━━\n" +
                "✅ Ro'yxatdan o'tish muvaffaqiyatli yakunlandi!\n\n" +
                "👤 {0}\n📱 {1}\n\n" +
                "🚛 <b>TruckBor</b> bilan biznesingizni yangi bosqichga olib chiqing!\n" +
                "━━━━━━━━━━━━━━━━━━━",
            [Language.UzCyrillic] =
                "🎉 <b>Хуш келибсиз, {0}!</b>\n\n" +
                "━━━━━━━━━━━━━━━━━━━\n" +
                "✅ Рўйхатдан ўтиш муваффақиятли якунланди!\n\n" +
                "👤 {0}\n📱 {1}\n\n" +
                "🚛 <b>TruckBor</b> билан бизнесингизни янги босқичга олиб чиқинг!\n" +
                "━━━━━━━━━━━━━━━━━━━",
            [Language.Russian] =
                "🎉 <b>Добро пожаловать, {0}!</b>\n\n" +
                "━━━━━━━━━━━━━━━━━━━\n" +
                "✅ Регистрация успешно завершена!\n\n" +
                "👤 {0}\n📱 {1}\n\n" +
                "🚛 Развивайте бизнес с <b>TruckBor</b>!\n" +
                "━━━━━━━━━━━━━━━━━━━",
            [Language.English] =
                "🎉 <b>Welcome, {0}!</b>\n\n" +
                "━━━━━━━━━━━━━━━━━━━\n" +
                "✅ Registration completed successfully!\n\n" +
                "👤 {0}\n📱 {1}\n\n" +
                "🚛 Grow your business with <b>TruckBor</b>!\n" +
                "━━━━━━━━━━━━━━━━━━━",
            [Language.Turkish] =
                "🎉 <b>Hoş geldiniz, {0}!</b>\n\n" +
                "━━━━━━━━━━━━━━━━━━━\n" +
                "✅ Kayıt başarıyla tamamlandı!\n\n" +
                "👤 {0}\n📱 {1}\n\n" +
                "🚛 <b>TruckBor</b> ile işinizi büyütün!\n" +
                "━━━━━━━━━━━━━━━━━━━",
        },

        // ── Subscription check ─────────────────────────────────────────────────
        ["subscription_check"] = new()
        {
            [Language.UzLatin]    = "📢 Botdan foydalanish uchun kanallarga a'zo bo'ling:",
            [Language.UzCyrillic] = "📢 Ботдан фойдаланиш учун каналларга аъзо бўлинг:",
            [Language.Russian]    = "📢 Для использования бота подпишитесь на каналы:",
            [Language.English]    = "📢 Subscribe to channels to use the bot:",
            [Language.Turkish]    = "📢 Botu kullanmak için kanallara abone olun:",
        },
        ["subscription_ok"] = new()
        {
            [Language.UzLatin]    = "✅ Rahmat! Botdan foydalanishingiz mumkin.",
            [Language.UzCyrillic] = "✅ Раҳмат! Ботдан фойдаланишингиз мумкин.",
            [Language.Russian]    = "✅ Спасибо! Вы можете пользоваться ботом.",
            [Language.English]    = "✅ Thank you! You can now use the bot.",
            [Language.Turkish]    = "✅ Teşekkürler! Botu artık kullanabilirsiniz.",
        },

        // ── Post flow ─────────────────────────────────────────────────────────
        ["no_subscription_for_post"] = new()
        {
            [Language.UzLatin]    = "❌ E'lon berish uchun <b>VIP obuna</b> kerak!\n\n⭐ Tarifni tanlang:",
            [Language.UzCyrillic] = "❌ Эълон бериш учун <b>VIP обуна</b> керак!\n\n⭐ Тарифни танланг:",
            [Language.Russian]    = "❌ Для подачи объявления нужна <b>VIP подписка</b>!\n\n⭐ Выберите тариф:",
            [Language.English]    = "❌ A <b>VIP subscription</b> is required to post ads!\n\n⭐ Choose a plan:",
            [Language.Turkish]    = "❌ İlan vermek için <b>VIP abonelik</b> gerekli!\n\n⭐ Tarif seçin:",
        },
        ["post_header"] = new()
        {
            [Language.UzLatin]    = "📍 <b>E'lon berish</b>",
            [Language.UzCyrillic] = "📍 <b>Эълон бериш</b>",
            [Language.Russian]    = "📍 <b>Подача объявления</b>",
            [Language.English]    = "📍 <b>Post an ad</b>",
            [Language.Turkish]    = "📍 <b>İlan ver</b>",
        },
        ["post_from_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerdan (shahar/tuman kiriting):",
            [Language.UzCyrillic] = "📍 Қаердан (шаҳар/туман киритинг):",
            [Language.Russian]    = "📍 Откуда (введите город/район):",
            [Language.English]    = "📍 From where (enter city/district):",
            [Language.Turkish]    = "📍 Nereden (şehir/ilçe girin):",
        },
        ["post_to_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerga (manzil kiriting):",
            [Language.UzCyrillic] = "📍 Қаерга (манзил киритинг):",
            [Language.Russian]    = "📍 Куда (введите адрес):",
            [Language.English]    = "📍 Where to (enter address):",
            [Language.Turkish]    = "📍 Nereye (adres girin):",
        },
        ["post_cargo_prompt"] = new()
        {
            [Language.UzLatin]    = "📦 Yuk turini kiriting (masalan: Piyoz, G'alla, Qum):",
            [Language.UzCyrillic] = "📦 Юк турини киритинг (масалан: Пиёз, Ғалла, Қум):",
            [Language.Russian]    = "📦 Введите тип груза (например: Лук, Зерно, Песок):",
            [Language.English]    = "📦 Enter cargo type (e.g., Onions, Grain, Sand):",
            [Language.Turkish]    = "📦 Yük türünü girin (örn: Soğan, Tahıl, Kum):",
        },
        ["post_weight_prompt"] = new()
        {
            [Language.UzLatin]    = "⚖️ Og'irligini kiriting (masalan: 10 tonna):",
            [Language.UzCyrillic] = "⚖️ Оғирлигини киритинг (масалан: 10 тонна):",
            [Language.Russian]    = "⚖️ Введите вес (например: 10 тонн):",
            [Language.English]    = "⚖️ Enter weight (e.g., 10 tons):",
            [Language.Turkish]    = "⚖️ Ağırlığı girin (örn: 10 ton):",
        },
        ["post_price_prompt"] = new()
        {
            [Language.UzLatin]    = "💰 Narxini kiriting (yoki 'Kelishiladi' deb yozing):",
            [Language.UzCyrillic] = "💰 Нархини киритинг (ёки 'Келишилади' деб ёзинг):",
            [Language.Russian]    = "💰 Введите цену (или напишите 'Договорная'):",
            [Language.English]    = "💰 Enter price (or write 'Negotiable'):",
            [Language.Turkish]    = "💰 Fiyatı girin (veya 'Pazarlıklı' yazın):",
        },
        ["post_contact_prompt"] = new()
        {
            [Language.UzLatin]    = "📞 Telefon raqamingizni kiriting:",
            [Language.UzCyrillic] = "📞 Телефон рақамингизни киритинг:",
            [Language.Russian]    = "📞 Введите номер телефона:",
            [Language.English]    = "📞 Enter your phone number:",
            [Language.Turkish]    = "📞 Telefon numaranızı girin:",
        },
        ["post_accepted"] = new()
        {
            [Language.UzLatin]    = "✅ <b>E'loningiz qabul qilindi!</b>\n\n⏳ E'lon guruxlarga tarqatilmoqda...",
            [Language.UzCyrillic] = "✅ <b>Эълонингиз қабул қилинди!</b>\n\n⏳ Эълон гуруҳларга тарқатилмоқда...",
            [Language.Russian]    = "✅ <b>Ваше объявление принято!</b>\n\n⏳ Объявление рассылается по группам...",
            [Language.English]    = "✅ <b>Your ad has been accepted!</b>\n\n⏳ The ad is being sent to groups...",
            [Language.Turkish]    = "✅ <b>İlanınız kabul edildi!</b>\n\n⏳ İlan gruplara dağıtılıyor...",
        },

        // ── Search ────────────────────────────────────────────────────────────
        ["search_title"] = new()
        {
            [Language.UzLatin]    = "🔍 <b>So'nggi e'lonlar:</b>\n\n",
            [Language.UzCyrillic] = "🔍 <b>Сўнгги эълонлар:</b>\n\n",
            [Language.Russian]    = "🔍 <b>Последние объявления:</b>\n\n",
            [Language.English]    = "🔍 <b>Latest ads:</b>\n\n",
            [Language.Turkish]    = "🔍 <b>Son ilanlar:</b>\n\n",
        },
        ["no_posts"] = new()
        {
            [Language.UzLatin]    = "📭 Hozircha e'lonlar yo'q.",
            [Language.UzCyrillic] = "📭 Ҳозирча эълонлар йўқ.",
            [Language.Russian]    = "📭 Объявлений пока нет.",
            [Language.English]    = "📭 No ads yet.",
            [Language.Turkish]    = "📭 Henüz ilan yok.",
        },

        // ── Help ──────────────────────────────────────────────────────────────
        ["help_text"] = new()
        {
            [Language.UzLatin] =
                "ℹ️ <b>Yordam</b>\n\n" +
                "📞 Admin: @TruckBorAdmin\n" +
                "📢 Kanal: @TruckBorUz\n" +
                "🌐 Sayt: truckbor.uz\n" +
                "📱 Mini App: app.truckbor.uz\n\n" +
                "🕐 Ish vaqti: 09:00 — 22:00",
            [Language.UzCyrillic] =
                "ℹ️ <b>Ёрдам</b>\n\n" +
                "📞 Админ: @TruckBorAdmin\n" +
                "📢 Канал: @TruckBorUz\n" +
                "🌐 Сайт: truckbor.uz\n" +
                "📱 Mini App: app.truckbor.uz\n\n" +
                "🕐 Иш вақти: 09:00 — 22:00",
            [Language.Russian] =
                "ℹ️ <b>Помощь</b>\n\n" +
                "📞 Админ: @TruckBorAdmin\n" +
                "📢 Канал: @TruckBorUz\n" +
                "🌐 Сайт: truckbor.uz\n" +
                "📱 Mini App: app.truckbor.uz\n\n" +
                "🕐 Время работы: 09:00 — 22:00",
            [Language.English] =
                "ℹ️ <b>Help</b>\n\n" +
                "📞 Admin: @TruckBorAdmin\n" +
                "📢 Channel: @TruckBorUz\n" +
                "🌐 Website: truckbor.uz\n" +
                "📱 Mini App: app.truckbor.uz\n\n" +
                "🕐 Working hours: 09:00 — 22:00",
            [Language.Turkish] =
                "ℹ️ <b>Yardım</b>\n\n" +
                "📞 Admin: @TruckBorAdmin\n" +
                "📢 Kanal: @TruckBorUz\n" +
                "🌐 Site: truckbor.uz\n" +
                "📱 Mini App: app.truckbor.uz\n\n" +
                "🕐 Çalışma saatleri: 09:00 — 22:00",
        },

        // ── Settings ──────────────────────────────────────────────────────────
        // {0} = language name
        ["settings_text"] = new()
        {
            [Language.UzLatin]    = "⚙️ <b>Sozlamalar</b>\n\n🌐 Til: {0}",
            [Language.UzCyrillic] = "⚙️ <b>Созламалар</b>\n\n🌐 Тил: {0}",
            [Language.Russian]    = "⚙️ <b>Настройки</b>\n\n🌐 Язык: {0}",
            [Language.English]    = "⚙️ <b>Settings</b>\n\n🌐 Language: {0}",
            [Language.Turkish]    = "⚙️ <b>Ayarlar</b>\n\n🌐 Dil: {0}",
        },
        // ── Post type names ────────────────────────────────────────────────
        ["post_type_cargo"] = new()
        {
            [Language.UzLatin]    = "Yuk e'loni",
            [Language.UzCyrillic] = "Юк эълони",
            [Language.Russian]    = "Грузовое объявление",
            [Language.English]    = "Cargo ad",
            [Language.Turkish]    = "Yük ilanı",
        },
        ["post_type_transport"] = new()
        {
            [Language.UzLatin]    = "Transport e'loni",
            [Language.UzCyrillic] = "Транспорт эълони",
            [Language.Russian]    = "Транспортное объявление",
            [Language.English]    = "Transport ad",
            [Language.Turkish]    = "Taşıt ilanı",
        },
        ["post_type_dogruz"] = new()
        {
            [Language.UzLatin]    = "Dogruz (qo'sh yuk)",
            [Language.UzCyrillic] = "Доғруз (қўш юк)",
            [Language.Russian]    = "Догруз (попутка)",
            [Language.English]    = "Extra cargo",
            [Language.Turkish]    = "Yol arkadaşı",
        },

        // ── Transport flow prompts ────────────────────────────────────────
        ["transport_from_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerdan (shahar kiriting):",
            [Language.UzCyrillic] = "📍 Қаердан (шаҳар киритинг):",
            [Language.Russian]    = "📍 Откуда (введите город):",
            [Language.English]    = "📍 From where (enter city):",
            [Language.Turkish]    = "📍 Nereden (şehir girin):",
        },
        ["transport_to_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerga (manzil kiriting):",
            [Language.UzCyrillic] = "📍 Қаерга (манзил киритинг):",
            [Language.Russian]    = "📍 Куда (введите адрес):",
            [Language.English]    = "📍 Where to (enter destination):",
            [Language.Turkish]    = "📍 Nereye (adres girin):",
        },
        ["transport_vehicle_prompt"] = new()
        {
            [Language.UzLatin]    = "🚗 Transport turini tanlang:",
            [Language.UzCyrillic] = "🚗 Транспорт турини танланг:",
            [Language.Russian]    = "🚗 Выберите тип транспорта:",
            [Language.English]    = "🚗 Choose vehicle type:",
            [Language.Turkish]    = "🚗 Araç türünü seçin:",
        },
        ["transport_capacity_prompt"] = new()
        {
            [Language.UzLatin]    = "⚖️ Yuk ko'tarish hajmini kiriting (masalan: 20 tonna):",
            [Language.UzCyrillic] = "⚖️ Юк кўтариш ҳажмини киритинг (масалан: 20 тонна):",
            [Language.Russian]    = "⚖️ Введите грузоподъёмность (например: 20 тонн):",
            [Language.English]    = "⚖️ Enter load capacity (e.g., 20 tons):",
            [Language.Turkish]    = "⚖️ Yük kapasitesini girin (örn: 20 ton):",
        },
        ["transport_price_prompt"] = new()
        {
            [Language.UzLatin]    = "💰 Narxini kiriting (yoki 'Kelishiladi'):",
            [Language.UzCyrillic] = "💰 Нархини киритинг (ёки 'Келишилади'):",
            [Language.Russian]    = "💰 Введите цену (или 'Договорная'):",
            [Language.English]    = "💰 Enter price (or 'Negotiable'):",
            [Language.Turkish]    = "💰 Fiyatı girin (veya 'Pazarlıklı'):",
        },

        // ── Dogruz flow prompts ───────────────────────────────────────────
        ["dogruz_from_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerdan (shahar kiriting):",
            [Language.UzCyrillic] = "📍 Қаердан (шаҳар киритинг):",
            [Language.Russian]    = "📍 Откуда (введите город):",
            [Language.English]    = "📍 From where (enter city):",
            [Language.Turkish]    = "📍 Nereden (şehir girin):",
        },
        ["dogruz_to_prompt"] = new()
        {
            [Language.UzLatin]    = "📍 Qayerga (manzil kiriting):",
            [Language.UzCyrillic] = "📍 Қаерга (манзил киритинг):",
            [Language.Russian]    = "📍 Куда (введите адрес):",
            [Language.English]    = "📍 Where to (enter destination):",
            [Language.Turkish]    = "📍 Nereye (adres girin):",
        },
        ["dogruz_capacity_prompt"] = new()
        {
            [Language.UzLatin]    = "📐 Bo'sh joy hajmi (masalan: 5 tonna):",
            [Language.UzCyrillic] = "📐 Бўш жой ҳажми (масалан: 5 тонна):",
            [Language.Russian]    = "📐 Свободное место (например: 5 тонн):",
            [Language.English]    = "📐 Available space (e.g., 5 tons):",
            [Language.Turkish]    = "📐 Boş alan (örn: 5 ton):",
        },
        ["dogruz_price_prompt"] = new()
        {
            [Language.UzLatin]    = "💰 Narxini kiriting (yoki 'Kelishiladi'):",
            [Language.UzCyrillic] = "💰 Нархини киритинг (ёки 'Келишилади'):",
            [Language.Russian]    = "💰 Введите цену (или 'Договорная'):",
            [Language.English]    = "💰 Enter price (or 'Negotiable'):",
            [Language.Turkish]    = "💰 Fiyatı girin (veya 'Pazarlıklı'):",
        },

        // ── Search prompts ────────────────────────────────────────────────
        ["search_from_prompt"] = new()
        {
            [Language.UzLatin]    = "🔍 Qayerdan (shahar kiriting yoki bo'sh qoldiring):",
            [Language.UzCyrillic] = "🔍 Қаердан (шаҳар киритинг ёки бўш қолдиринг):",
            [Language.Russian]    = "🔍 Откуда (введите город или оставьте пустым):",
            [Language.English]    = "🔍 From where (enter city or leave empty):",
            [Language.Turkish]    = "🔍 Nereden (şehir girin veya boş bırakın):",
        },
        ["search_to_prompt"] = new()
        {
            [Language.UzLatin]    = "🔍 Qayerga (manzil kiriting yoki bo'sh qoldiring):",
            [Language.UzCyrillic] = "🔍 Қаерга (манзил киритинг ёки бўш қолдиринг):",
            [Language.Russian]    = "🔍 Куда (введите город или оставьте пустым):",
            [Language.English]    = "🔍 Where to (enter city or leave empty):",
            [Language.Turkish]    = "🔍 Nereye (şehir girin veya boş bırakın):",
        },

        // ── AI flow ───────────────────────────────────────────────────────
        ["ai_parse_failed"] = new()
        {
            [Language.UzLatin]    = "🤖 AI matnni tahlil qila olmadi.\n\n📝 Qo'lda kiritamiz:",
            [Language.UzCyrillic] = "🤖 AI матнни таҳлил қила олмади.\n\n📝 Қўлда киритамиз:",
            [Language.Russian]    = "🤖 AI не смог распознать текст.\n\n📝 Заполним вручную:",
            [Language.English]    = "🤖 AI couldn't parse the text.\n\n📝 Let's fill in manually:",
            [Language.Turkish]    = "🤖 AI metni çözümleyemedi.\n\n📝 Manuel dolduralım:",
        },
        ["matching_header"] = new()
        {
            [Language.UzLatin]    = "🎯 <b>Sizga mos e'lonlar:</b>",
            [Language.UzCyrillic] = "🎯 <b>Сизга мос эълонлар:</b>",
            [Language.Russian]    = "🎯 <b>Подходящие объявления:</b>",
            [Language.English]    = "🎯 <b>Matching ads:</b>",
            [Language.Turkish]    = "🎯 <b>Uygun ilanlar:</b>",
        },
        ["settings_change_lang"] = new()
        {
            [Language.UzLatin]    = "🌐 Tilni o'zgartirish",
            [Language.UzCyrillic] = "🌐 Тилни ўзгартириш",
            [Language.Russian]    = "🌐 Изменить язык",
            [Language.English]    = "🌐 Change language",
            [Language.Turkish]    = "🌐 Dili değiştir",
        },
    };

    public string Get(string key, Language language)
    {
        if (_strings.TryGetValue(key, out var langs))
            if (langs.TryGetValue(language, out var text))
                return text;
        return key;
    }

    public string Get(string key, Language language, params object[] args)
        => string.Format(Get(key, language), args);
}
