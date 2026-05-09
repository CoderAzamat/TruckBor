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
        // PostgreSQL
        services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp =>
            sp.GetRequiredService<AppDbContext>());

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("Redis")!));

        // Services
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IUserStateService, UserStateService>();
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPostingService, PostingService>();
        services.AddSingleton<IAiPostService, AiPostService>();

        // Telegram Bot
        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(configuration["Bot:Token"]!));

        // Bot Handler
        services.AddScoped<BotUpdateHandler>();

        return services;
    }
}