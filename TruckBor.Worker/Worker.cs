using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Data;
using TruckBor.Infrastructure.Services;

namespace TruckBor.Worker;

/// <summary>
/// Background worker: subscription expiry, post expiry, analytics,
/// session health checks, spam recovery, account maintenance.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient   _bot;
    private readonly ILogger<Worker>      _logger;

    private DateTime _lastSubscriptionCheck = DateTime.MinValue;
    private DateTime _lastSessionCheck      = DateTime.MinValue;
    private DateTime _lastSpamRecovery      = DateTime.MinValue;
    private DateTime _lastScrape            = DateTime.MinValue;
    private int      _analyticsMinute       = -1;

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

                // ── Session health check: every 2 hours ───────────────────
                if ((now - _lastSessionCheck).TotalHours >= 2)
                {
                    _lastSessionCheck = now;
                    await CheckSessionHealthAsync(ct);
                }

                // ── Spam recovery: every 1 hour ───────────────────────────
                if ((now - _lastSpamRecovery).TotalHours >= 1)
                {
                    _lastSpamRecovery = now;
                    await RecoverSpammedAccountsAsync(ct);
                }

                // ── Scraping: every 10 minutes ───────────────────────────
                if ((now - _lastScrape).TotalMinutes >= 10)
                {
                    _lastScrape = now;
                    await RunScrapingAsync(ct);
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

    // ═══ SESSION HEALTH CHECK ════════════════════════════════════════════
    private async Task CheckSessionHealthAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pyro = scope.ServiceProvider.GetRequiredService<ITelegramSmsAuthService>();

            var accounts = await db.TelegramAccounts
                .Where(x => x.IsActive && !x.IsSpammed &&
                            x.SessionString != null && x.SessionString != "")
                .ToListAsync(ct);

            if (!accounts.Any()) return;

            var invalidCount = 0;
            foreach (var account in accounts)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await pyro.CheckSessionAsync(account.SessionString!, ct);
                    if (result.Success && !result.Valid)
                    {
                        account.IsActive = false;
                        invalidCount++;
                        _logger.LogWarning("Session invalid for {Phone}, deactivated", account.PhoneNumber);

                        // Notify user
                        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == account.UserId, ct);
                        if (user is not null)
                        {
                            try
                            {
                                await _bot.SendMessage(user.TelegramId,
                                    $"⚠️ <b>Akkaunt sessiyasi eskirgan!</b>\n\n" +
                                    $"📞 {account.PhoneNumber}\n" +
                                    "Qayta ulanish uchun «📱 Akkaunt qo'shish» tugmasini bosing.",
                                    parseMode: ParseMode.Html, cancellationToken: ct);
                            }
                            catch { }
                        }
                    }
                    else if (result.Success && result.IsPremium && !account.IsPremium)
                    {
                        account.IsPremium = true; // sync Premium status
                    }

                    await Task.Delay(500, ct); // rate limit
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Session check failed for {Phone}", account.PhoneNumber);
                }
            }

            if (invalidCount > 0) await db.SaveChangesAsync(ct);
            _logger.LogInformation("Session health check: {Total} accounts, {Invalid} invalid",
                accounts.Count, invalidCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Session health check failed");
        }
    }

    // ═══ SPAM RECOVERY ══════════════════════════════════════════════════
    private async Task RecoverSpammedAccountsAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pyro = scope.ServiceProvider.GetRequiredService<ITelegramSmsAuthService>();

            // Try to recover accounts spammed more than 1 hour ago
            var cutoff = DateTime.UtcNow.AddHours(-1);
            var spammedAccounts = await db.TelegramAccounts
                .Where(x => x.IsSpammed && x.SpammedAt != null && x.SpammedAt < cutoff &&
                            x.SessionString != null && x.SessionString != "")
                .Take(5) // limit batch size
                .ToListAsync(ct);

            if (!spammedAccounts.Any()) return;

            var recovered = 0;
            foreach (var account in spammedAccounts)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    // Try fix spam via @SpamBot
                    var fix = await pyro.FixSpamAsync(account.SessionString!, ct);

                    // Check if session is still valid and not spammed
                    var check = await pyro.CheckSessionAsync(account.SessionString!, ct);
                    if (check.Success && check.Valid)
                    {
                        account.IsSpammed = false;
                        account.IsActive = true;
                        account.SpammedAt = null;
                        recovered++;

                        // Notify user
                        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == account.UserId, ct);
                        if (user is not null)
                        {
                            try
                            {
                                await _bot.SendMessage(user.TelegramId,
                                    $"✅ <b>Akkaunt tiklandi!</b>\n\n" +
                                    $"📞 {account.PhoneNumber}\n" +
                                    "Spam blokdan chiqarildi. Endi e'lon tarqatish uchun foydalanish mumkin!",
                                    parseMode: ParseMode.Html, cancellationToken: ct);
                            }
                            catch { }
                        }

                        _logger.LogInformation("Account {Phone} recovered from spam", account.PhoneNumber);
                    }

                    await Task.Delay(2000, ct); // don't rush
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Spam recovery failed for {Phone}", account.PhoneNumber);
                }
            }

            if (recovered > 0) await db.SaveChangesAsync(ct);
            _logger.LogInformation("Spam recovery: {Recovered}/{Total} accounts recovered",
                recovered, spammedAccounts.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Spam recovery failed");
        }
    }

    // ═══ SCRAPING ═══════════════════════════════════════════════════════
    private async Task RunScrapingAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var scraper = scope.ServiceProvider.GetRequiredService<ScrapingService>();
            await scraper.ScrapeAllGroupsAsync(ct);
            await scraper.CleanupExpiredAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scraping failed");
        }
    }
}
