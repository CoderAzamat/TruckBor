using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Telegram.Bot;
using TruckBor.Application.Interfaces;
using TruckBor.Infrastructure.Data;
using TruckBor.Infrastructure.Services;
using TruckBor.Infrastructure.Telegram.Handlers;

namespace TruckBor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── PostgreSQL ──────────────────────────────────────────
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp =>
            sp.GetRequiredService<AppDbContext>());

        // ── Redis ───────────────────────────────────────────────
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")!));

        // ── HTTP clients ────────────────────────────────────────
        services.AddHttpClient("smsactivate", client =>
        {
            client.BaseAddress = new Uri("https://api.sms-activate.guru");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("pyro", client =>
        {
            var url = configuration["TelegramAuth:ServiceUrl"];
            if (!string.IsNullOrWhiteSpace(url))
                client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("telegram");

        // ── Core Services ───────────────────────────────────────
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IUserStateService, UserStateService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IAiPostService, AiPostService>();

        // ── Scoped Business Services ────────────────────────────
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPostingService, PostingService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IVirtualNumberService, VirtualNumberService>();
        services.AddScoped<IPremiumOrderService, PremiumOrderService>();
        services.AddScoped<ITelegramSmsAuthService, TelegramSmsAuthService>();

        // ── Telegram Bot ────────────────────────────────────────
        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(configuration["Bot:Token"]!));

        // ── Bot Handler ─────────────────────────────────────────
        services.AddScoped<BotUpdateHandler>();

        return services;
    }
}
