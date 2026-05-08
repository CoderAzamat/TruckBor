using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Data;

namespace TruckBor.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, ITelegramBotClient bot, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TruckBor Worker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await ExpireSubscriptionsAsync(db, ct);
                await ExpirePostsAsync(db, ct);
                await NotifyExpiringSubscriptionsAsync(db, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), ct);
        }
    }

    private async Task ExpireSubscriptionsAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db.Subscriptions
            .Where(x => x.Status == SubscriptionStatus.Active && x.EndDate <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (!expired.Any()) return;

        foreach (var sub in expired)
            sub.Status = SubscriptionStatus.Expired;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} subscriptions", expired.Count);
    }

    private async Task ExpirePostsAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db.Posts
            .Where(x => x.Status == PostStatus.Active && x.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (!expired.Any()) return;

        foreach (var post in expired)
            post.Status = PostStatus.Expired;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} posts", expired.Count);
    }

    private async Task NotifyExpiringSubscriptionsAsync(AppDbContext db, CancellationToken ct)
    {
        var in3Days = DateTime.UtcNow.AddDays(3);
        var in3DaysStart = in3Days.Date;
        var in3DaysEnd = in3DaysStart.AddDays(1);

        var subs = await db.Subscriptions
            .Include(x => x.User)
            .Include(x => x.Tariff)
            .Where(x => x.Status == SubscriptionStatus.Active &&
                x.EndDate >= in3DaysStart &&
                x.EndDate < in3DaysEnd)
            .ToListAsync(ct);

        foreach (var sub in subs)
        {
            if (sub.User is null) continue;
            try
            {
                var msg =
                    $"⚠️ <b>Obunangiz tugayapti!</b>\n\n" +
                    $"⭐ Tarif: {sub.Tariff?.Name}\n" +
                    $"📅 Tugash: {sub.EndDate:dd.MM.yyyy}\n" +
                    $"⏰ Qoldi: {sub.DaysLeft} kun\n\n" +
                    "Uzluksiz foydalanish uchun tarifni yangilang! 🚀";

                await _bot.SendMessage(sub.User.TelegramId, msg,
                    parseMode: ParseMode.Html, cancellationToken: ct);
                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not notify user {UserId}", sub.User.TelegramId);
            }
        }

        if (subs.Any())
            _logger.LogInformation("Sent expiry notifications to {Count} users", subs.Count);
    }
}
