using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Domain.Entities;
using TruckBor.Infrastructure.Data;

namespace TruckBor.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.MigrateAsync();

            // Tariflar
            if (!await db.Tariffs.AnyAsync())
            {
                await db.Tariffs.AddRangeAsync(new List<Tariff>
                {
                    new()
                    {
                        Name = "Starter",
                        Description = "Boshlang'ich tarif",
                        Price = 99_000,
                        DurationDays = 30,
                        MaxAccounts = 1,
                        MaxGroups = 200,
                        PostsPerDay = 2,
                        PostIntervalMinutes = 60,
                        IsActive = true,
                        SortOrder = 1
                    },
                    new()
                    {
                        Name = "Business",
                        Description = "Biznes tarif",
                        Price = 199_000,
                        DurationDays = 30,
                        MaxAccounts = 3,
                        MaxGroups = 500,
                        PostsPerDay = 4,
                        PostIntervalMinutes = 30,
                        IsRecommended = true,
                        IsActive = true,
                        SortOrder = 2
                    },
                    new()
                    {
                        Name = "Pro",
                        Description = "Professional tarif",
                        Price = 349_000,
                        DurationDays = 30,
                        MaxAccounts = 5,
                        MaxGroups = 1000,
                        PostsPerDay = 8,
                        PostIntervalMinutes = 15,
                        IsActive = true,
                        SortOrder = 3
                    },
                    new()
                    {
                        Name = "Enterprise",
                        Description = "Korporativ tarif",
                        Price = 699_000,
                        DurationDays = 30,
                        MaxAccounts = 10,
                        MaxGroups = 2000,
                        PostsPerDay = 999,
                        PostIntervalMinutes = 5,
                        IsActive = true,
                        SortOrder = 4
                    }
                });
            }

            // Sozlamalar
            if (!await db.Settings.AnyAsync())
            {
                await db.Settings.AddRangeAsync(new List<Setting>
                {
                    new() { Key = "welcome_uz", Value = "🚛 TruckBor ga xush kelibsiz!" },
                    new() { Key = "welcome_ru", Value = "🚛 Добро пожаловать в TruckBor!" },
                    new() { Key = "welcome_en", Value = "🚛 Welcome to TruckBor!" },
                    new() { Key = "welcome_tr", Value = "🚛 TruckBor'a hoş geldiniz!" },
                    new() { Key = "support_username", Value = "@TruckBorAdmin" },
                    new() { Key = "channel_username", Value = "@TruckBorUz" },
                    new() { Key = "maintenance_mode", Value = "false" },
                    new() { Key = "maintenance_text_uz", Value = "🔧 Tizim yangilanmoqda. Tez orada qaytamiz!" },
                    new() { Key = "maintenance_text_ru", Value = "🔧 Система обновляется. Скоро вернёмся!" },
                    new() { Key = "post_channel_id", Value = "0" },
                    new() { Key = "auto_post_interval_minutes", Value = "1" }
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