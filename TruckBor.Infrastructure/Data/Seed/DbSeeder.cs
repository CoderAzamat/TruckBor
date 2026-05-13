using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Domain.Entities;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Data;

namespace TruckBor.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.MigrateAsync();

            // ── Tariflar ──────────────────────────────────────
            if (!await db.Tariffs.AnyAsync())
            {
                await db.Tariffs.AddRangeAsync(new List<Tariff>
                {
                    new() { Name = "Starter",    Description = "Boshlang'ich tarif",   Price = 99_000,  DurationDays = 30, MaxAccounts = 1,  MaxGroups = 200,  PostsPerDay = 2, PostIntervalMinutes = 60, IsActive = true, SortOrder = 1 },
                    new() { Name = "Business",   Description = "Biznes tarif",          Price = 199_000, DurationDays = 30, MaxAccounts = 3,  MaxGroups = 500,  PostsPerDay = 4, PostIntervalMinutes = 30, IsRecommended = true, IsActive = true, SortOrder = 2 },
                    new() { Name = "Pro",        Description = "Professional tarif",    Price = 349_000, DurationDays = 30, MaxAccounts = 5,  MaxGroups = 1000, PostsPerDay = 8, PostIntervalMinutes = 15, IsActive = true, SortOrder = 3 },
                    new() { Name = "Enterprise", Description = "Korporativ tarif",      Price = 699_000, DurationDays = 30, MaxAccounts = 10, MaxGroups = 2000, PostsPerDay = 999, PostIntervalMinutes = 5, IsActive = true, SortOrder = 4 },
                });
            }

            // ── To'lov provayderlari ──────────────────────────
            if (!await db.PaymentProviders.AnyAsync())
            {
                await db.PaymentProviders.AddRangeAsync(new List<PaymentProvider>
                {
                    new() { Code = "card",  DisplayName = "Karta orqali",         IconEmoji = "💳", Type = PaymentType.Manual, IsActive = true,  SortOrder = 1, MinAmount = 10_000, MaxAmount = 50_000_000 },
                    new() { Code = "click", DisplayName = "Click",                 IconEmoji = "🟢", Type = PaymentType.Click,  IsActive = false, SortOrder = 2, MinAmount = 1_000,  MaxAmount = 10_000_000 },
                    new() { Code = "payme", DisplayName = "Payme",                 IconEmoji = "🔵", Type = PaymentType.Payme,  IsActive = false, SortOrder = 3, MinAmount = 1_000,  MaxAmount = 10_000_000 },
                    new() { Code = "uzum",  DisplayName = "Uzum Bank",             IconEmoji = "🟣", Type = PaymentType.Uzum,   IsActive = false, SortOrder = 4, MinAmount = 1_000,  MaxAmount = 10_000_000 },
                    new() { Code = "stars", DisplayName = "Telegram Stars",        IconEmoji = "⭐", Type = PaymentType.Stars,  IsActive = true,  SortOrder = 5, MinAmount = 1,      MaxAmount = 10_000 },
                });
            }

            // ── Sozlamalar ────────────────────────────────────
            if (!await db.Settings.AnyAsync())
            {
                var settings = new List<Setting>
                {
                    // Xush kelibsiz xabar
                    new() { Key = "welcome_uz",  Value = "🚛 <b>TruckBor</b>ga xush kelibsiz!\n\n🇺🇿 O'zbekistonning yetakchi yuk tashish platformasi" },
                    new() { Key = "welcome_ru",  Value = "🚛 Добро пожаловать в <b>TruckBor</b>!\n\n🇺🇿 Ведущая платформа грузоперевозок Узбекистана" },
                    new() { Key = "welcome_en",  Value = "🚛 Welcome to <b>TruckBor</b>!\n\n🇺🇿 Uzbekistan's leading cargo platform" },
                    new() { Key = "welcome_tr",  Value = "🚛 <b>TruckBor</b>'a hoş geldiniz!\n\n🇺🇿 Özbekistan'ın önde gelen nakliyat platformu" },
                    new() { Key = "welcome_uzc", Value = "🚛 <b>TruckBor</b>га хуш келибсиз!\n\n🇺🇿 Ўзбекистоннинг йетакчи юк ташиш платформаси" },

                    // Bot sozlamalari
                    new() { Key = "support_username",    Value = "@TruckBorAdmin" },
                    new() { Key = "channel_username",    Value = "@TruckBorUz" },
                    new() { Key = "mini_app_url",        Value = "https://t.me/TruckBorBot/app" },
                    new() { Key = "maintenance_mode",    Value = "false" },
                    new() { Key = "maintenance_text_uz", Value = "🔧 Tizim yangilanmoqda. Tez orada qaytamiz!" },
                    new() { Key = "maintenance_text_ru", Value = "🔧 Система обновляется. Скоро вернёмся!" },

                    // Kanal va analitika
                    new() { Key = "post_channel_id",           Value = "0" },
                    new() { Key = "analytics_channel_id",      Value = "0" },
                    new() { Key = "auto_post_interval_minutes", Value = "1" },

                    // Virtual raqamlar
                    new() { Key = "smsactivate_api_key", Value = "" },
                    new() { Key = "vnumber_uz_price",    Value = "2000" },
                    new() { Key = "vnumber_ru_price",    Value = "1500" },
                    new() { Key = "vnumber_kz_price",    Value = "1800" },
                    new() { Key = "vnumber_uk_price",    Value = "3000" },
                    new() { Key = "vnumber_in_price",    Value = "800" },
                    new() { Key = "vnumber_pl_price",    Value = "2500" },

                    // Telegram Premium narxlari
                    new() { Key = "premium_1month_price",  Value = "99000" },
                    new() { Key = "premium_3month_price",  Value = "249000" },
                    new() { Key = "premium_6month_price",  Value = "449000" },
                    new() { Key = "premium_12month_price", Value = "799000" },

                    // Telegram Stars narxlari
                    new() { Key = "stars_per_som",      Value = "5" },  // 1 Stars = 5 so'm
                    new() { Key = "tariff_stars_1",     Value = "50" }, // Starter = 50 Stars
                    new() { Key = "tariff_stars_2",     Value = "100" },
                    new() { Key = "tariff_stars_3",     Value = "175" },
                    new() { Key = "tariff_stars_4",     Value = "350" },
                };
                await db.Settings.AddRangeAsync(settings);
            }

            // ── Default Super Admin ───────────────────────────
            if (!await db.AdminUsers.AnyAsync())
            {
                db.AdminUsers.Add(new AdminUser
                {
                    TelegramId          = 0,
                    FullName            = "Super Admin",
                    Username            = "admin",
                    PasswordHash        = BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 12),
                    IsSuper             = true,
                    CanManageUsers      = true,
                    CanManagePayments   = true,
                    CanManageTariffs    = true,
                    CanManageGroups     = true,
                    CanManageCards      = true,
                    CanManageChannels   = true,
                    CanBroadcast        = true,
                    CanViewStatistics   = true,
                    CanManageAdmins     = true,
                    CanManageSettings   = true,
                    CanManageVirtual    = true,
                    CanManagePremium    = true,
                    CanManageVideos     = true,
                });
            }

            await db.SaveChangesAsync();
            logger.LogInformation("✅ Database seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Database seeding failed");
            throw;
        }
    }
}
