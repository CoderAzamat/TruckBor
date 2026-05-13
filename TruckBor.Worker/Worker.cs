using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Data;

namespace TruckBor.Worker;

/// <summary>
/// Background worker: subscription expiry, post expiry, analytics channel every minute.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient   _bot;
    private readonly ILogger<Worker>      _logger;

    private DateTime _lastSubscriptionCheck = DateTime.MinValue;
    private DateTime _lastAnalyticsPost     = DateTime.MinValue;
    private int      _analyticsMinute       = -1; // track last sent minute

    public Worker(IServiceScopeFactory scopeFactory, ITelegramBotClient bot, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory; _bot = bot; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TruckBor Worker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // ── Analytics channel: every minute ───────────────────────
                if (now.Minute != _analyticsMinute)
                {
                    _analyticsMinute = now.Minute;
                    await PostAnalyticsAsync(ct);
                }

                // ── Subscription/post maintenance: every 30 min ───────────
                if ((now - _lastSubscriptionCheck).TotalMinutes >= 30)
                {
                    _lastSubscriptionCheck = now;
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await ExpireSubscriptionsAsync(db, ct);
                    await ExpirePostsAsync(db, ct);
                    await NotifyExpiringSubscriptionsAsync(db, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(55), ct); // tick ~every minute
        }
    }

    // ═══ ANALYTICS CHANNEL ═══════════════════════════════════════════════
    private async Task PostAnalyticsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Get analytics channel ID from settings
            var setting = await db.Settings.FirstOrDefaultAsync(x => x.Key == "analytics_channel_id", ct);
            if (setting is null || !long.TryParse(setting.Value, out var channelId) || channelId == 0) return;

            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); }
            catch { tz = TimeZoneInfo.Utc; }
            var tashkent = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var today = DateTime.UtcNow.Date;

            var totalUsers  = await db.Users.CountAsync(ct);
            var todayUsers  = await db.Users.CountAsync(x => x.CreatedAt >= today, ct);
            var totalPosts  = await db.Posts.CountAsync(ct);
            var todayPosts  = await db.Posts.CountAsync(x => x.CreatedAt >= today, ct);
            var activeSubs  = await db.Subscriptions.CountAsync(x =>
                x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);
            var pendingPay  = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Pending, ct);
            var todayRev    = await db.Payments
                .Where(x => x.Status == PaymentStatus.Approved && x.ApprovedAt >= today)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var totalRev    = await db.Payments
                .Where(x => x.Status == PaymentStatus.Approved)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var totalAcc    = await db.TelegramAccounts.CountAsync(ct);
            var activeAcc   = await db.TelegramAccounts.CountAsync(x => x.IsActive, ct);
            var totalGroups = await db.Groups.CountAsync(ct);

            var msg =
                $"📊 <b>TruckBor — Analitika</b>\n" +
                $"🕐 {tashkent:dd.MM.yyyy HH:mm:ss} (UTC+5)\n\n" +
                $"━━━━ 👥 FOYDALANUVCHILAR ━━━━\n" +
                $"• Jami: <b>{totalUsers:N0}</b> ta\n" +
                $"• Bugun qo'shildi: <b>+{todayUsers}</b>\n" +
                $"• Faol obuna: <b>{activeSubs}</b>\n\n" +
                $"━━━━ 📦 E'LONLAR ━━━━\n" +
                $"• Bugun: <b>{todayPosts}</b> ta\n" +
                $"• Jami: <b>{totalPosts:N0}</b> ta\n\n" +
                $"━━━━ 💰 MOLIYA ━━━━\n" +
                $"• Bugun: <b>{todayRev:N0}</b> so'm\n" +
                $"• Jami: <b>{totalRev:N0}</b> so'm\n" +
                $"• Kutmoqda: <b>{pendingPay}</b> ta\n\n" +
                $"━━━━ 📱 AKKAUNTLAR ━━━━\n" +
                $"• Faol: <b>{activeAcc}</b> / {totalAcc}\n" +
                $"• Guruhlar: <b>{totalGroups:N0}</b>\n\n" +
                $"━━━━ 🖥️ SERVER ━━━━\n" +
                $"• Xotira: <b>{GC.GetTotalMemory(false) / 1024 / 1024} MB</b>\n" +
                $"• Status: <b>✅ Ishlayapti</b>\n\n" +
                $"<i>#TruckBor #Analytics</i>";

            await _bot.SendMessage(channelId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Analytics post failed (channel may not be configured)");
        }
    }

    // ═══ SUBSCRIPTION EXPIRY ═════════════════════════════════════════════
    private async Task ExpireSubscriptionsAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db.Subscriptions
            .Where(x => x.Status == SubscriptionStatus.Active && x.EndDate <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (!expired.Any()) return;

        foreach (var sub in expired) sub.Status = SubscriptionStatus.Expired;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} subscriptions", expired.Count);
    }

    private async Task ExpirePostsAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db.Posts
            .Where(x => x.Status == PostStatus.Active && x.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (!expired.Any()) return;

        foreach (var post in expired) post.Status = PostStatus.Expired;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} posts", expired.Count);
    }

    private async Task NotifyExpiringSubscriptionsAsync(AppDbContext db, CancellationToken ct)
    {
        var in3Days    = DateTime.UtcNow.AddDays(3);
        var in3DaysEnd = in3Days.Date.AddDays(1);

        var subs = await db.Subscriptions
            .Include(x => x.User)
            .Include(x => x.Tariff)
            .Where(x => x.Status == SubscriptionStatus.Active &&
                x.EndDate >= in3Days.Date && x.EndDate < in3DaysEnd)
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
                    "Uzluksiz foydalanish uchun obunani yangilang! 🚀";

                await _bot.SendMessage(sub.User.TelegramId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
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
