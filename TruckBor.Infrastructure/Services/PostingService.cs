using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Services;

public class PostingService : IPostingService
{
    private readonly IAppDbContext _db;
    private readonly ITelegramBotClient _bot;
    private readonly ITelegramSmsAuthService _pyro;
    private readonly ILogger<PostingService> _logger;

    public PostingService(
        IAppDbContext db,
        ITelegramBotClient bot,
        ITelegramSmsAuthService pyro,
        ILogger<PostingService> logger)
    {
        _db = db; _bot = bot; _pyro = pyro; _logger = logger;
    }

    public async Task PostToGroupsAsync(long postId, long userId, CancellationToken ct = default)
    {
        // Always post to channel via bot first
        await PostToChannelAsync(postId, ct);

        var post = await _db.Posts
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == postId, ct);
        if (post is null) return;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return;

        var sub = await _db.Subscriptions
            .Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.UserId == userId &&
                x.Status == SubscriptionStatus.Active &&
                x.EndDate > DateTime.UtcNow, ct);

        if (sub?.Tariff is null) return;

        var tariff  = sub.Tariff;
        var caption = BuildCaption(post, user);

        var groups = await _db.Groups
            .Where(x => x.IsActive && x.MinTariffLevel <= tariff.SortOrder)
            .OrderBy(x => x.LastPostedAt ?? DateTime.MinValue)
            .Take(tariff.MaxGroups)
            .ToListAsync(ct);

        if (!groups.Any()) return;

        // ── Try posting from USER ACCOUNTS first (Pyrogram) ────────────────
        var accounts = await _db.TelegramAccounts
            .Where(x => x.UserId == userId && x.IsActive && !x.IsSpammed &&
                         x.SessionString != null && x.SessionString != "")
            .OrderBy(x => x.PostsSent)
            .ToListAsync(ct);

        if (accounts.Any())
        {
            await PostViaUserAccountsAsync(post, user, accounts, groups, caption, tariff, ct);
        }
        else
        {
            // Fallback: post via BOT API (bot must be admin in groups)
            await PostViaBotApiAsync(post, user, groups, caption, tariff, ct);
        }
    }

    // ═══ POST VIA USER ACCOUNTS (Pyrogram) ═══════════════════════════════════
    private async Task PostViaUserAccountsAsync(
        Domain.Entities.Post post,
        Domain.Entities.User user,
        List<Domain.Entities.TelegramAccount> accounts,
        List<Domain.Entities.Group> groups,
        string caption,
        Domain.Entities.Tariff tariff,
        CancellationToken ct)
    {
        var total = groups.Count;
        var posted = 0;
        var accountIndex = 0;
        var groupsPerAccount = Math.Max(total / Math.Max(accounts.Count, 1), 10);

        // Progress message
        int? progressMsgId = null;
        try
        {
            var initMsg = await _bot.SendMessage(user.TelegramId,
                $"📤 <b>E'lon tarqatilmoqda...</b>\n" +
                $"👥 {accounts.Count} ta akkaunt ishlatilmoqda\n" +
                $"⏳ 0/{total} guruhga yuborildi",
                parseMode: ParseMode.Html, cancellationToken: ct);
            progressMsgId = initMsg.MessageId;
        }
        catch { }

        foreach (var account in accounts)
        {
            if (ct.IsCancellationRequested || posted >= total) break;

            var batchGroups = groups
                .Skip(posted)
                .Take(groupsPerAccount)
                .Select(g => g.TelegramGroupId)
                .ToList();

            if (!batchGroups.Any()) break;

            _logger.LogInformation("Posting via account {Phone} to {Count} groups",
                account.PhoneNumber, batchGroups.Count);

            var result = await _pyro.SendToGroupsAsync(
                account.SessionString!,
                batchGroups,
                caption,
                delaySeconds: Math.Max(tariff.PostIntervalMinutes * 60 / Math.Max(batchGroups.Count, 1), 2),
                ct);

            if (result.Success)
            {
                posted += result.Sent;
                account.PostsSent += result.Sent;
                account.LastUsed = DateTime.UtcNow;

                // Handle spam detection
                if (result.Spam)
                {
                    account.IsSpammed = true;
                    account.SpammedAt = DateTime.UtcNow;
                    account.IsActive = false;
                    _logger.LogWarning("Account {Phone} got SPAMMED during posting", account.PhoneNumber);

                    // Auto-fix spam
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var fix = await _pyro.FixSpamAsync(account.SessionString!);
                            await _bot.SendMessage(user.TelegramId,
                                $"⚠️ <b>Spam aniqlandi!</b>\n\n" +
                                $"📞 {account.PhoneNumber}\n" +
                                $"🤖 @SpamBot ga avtomatik murojaat yuborildi.\n" +
                                (fix.IsPremium
                                    ? "✅ Premium akkaunt — spam tez hal bo'ladi!"
                                    : "💡 <b>Maslahat:</b> Telegram Premium olsangiz spam yo'qoladi!"),
                                parseMode: ParseMode.Html);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Auto spam fix failed for {Phone}", account.PhoneNumber);
                        }
                    }, CancellationToken.None);
                }

                // Update progress
                if (progressMsgId.HasValue)
                {
                    try
                    {
                        await _bot.EditMessageText(user.TelegramId, progressMsgId.Value,
                            $"📤 <b>E'lon tarqatilmoqda...</b>\n" +
                            $"✅ {posted}/{total} guruhga yuborildi\n" +
                            $"📱 {account.PhoneNumber} — {result.Sent} ta",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                    }
                    catch { }
                }

                // Mark failed groups as inactive
                if (result.Results is not null)
                {
                    foreach (var r in result.Results.Where(r => !r.Ok && r.Error is "banned/forbidden"))
                    {
                        var g = groups.FirstOrDefault(x => x.TelegramGroupId == r.GroupId);
                        if (g is not null) g.IsActive = false;
                    }
                }
            }
            else
            {
                _logger.LogWarning("SendToGroups failed for {Phone}: {Error}",
                    account.PhoneNumber, result.Error);
            }

            accountIndex++;
        }

        await _db.SaveChangesAsync(ct);

        // Final status
        if (progressMsgId.HasValue)
        {
            try
            {
                await _bot.EditMessageText(user.TelegramId, progressMsgId.Value,
                    $"✅ <b>E'lon muvaffaqiyatli tarqatildi!</b>\n" +
                    $"📤 {posted}/{total} guruhga yuborildi\n" +
                    $"👥 {accounts.Count} ta akkaunt ishlatildi\n" +
                    $"#TruckBor #{post.FromCity?.Replace(" ", "")} #{post.ToCity?.Replace(" ", "")}",
                    parseMode: ParseMode.Html, cancellationToken: ct);
            }
            catch { }
        }

        _logger.LogInformation("Post {PostId} sent to {Count}/{Total} groups via user accounts",
            post.Id, posted, total);
    }

    // ═══ POST VIA BOT API (fallback) ═════════════════════════════════════════
    private async Task PostViaBotApiAsync(
        Domain.Entities.Post post,
        Domain.Entities.User user,
        List<Domain.Entities.Group> groups,
        string caption,
        Domain.Entities.Tariff tariff,
        CancellationToken ct)
    {
        var total    = groups.Count;
        var delayMs  = Math.Max(tariff.PostIntervalMinutes * 60_000 / Math.Max(total, 1), 500);
        var posted   = 0;
        var progressEvery = Math.Max(total / 5, 1);

        int? progressMsgId = null;
        try
        {
            var initMsg = await _bot.SendMessage(user.TelegramId,
                $"📤 <b>E'lon tarqatilmoqda (bot orqali)...</b>\n" +
                $"⏳ 0/{total} guruhga yuborildi\n\n" +
                "💡 Akkaunt qo'shsangiz tezroq va ko'proq tarqatiladi!",
                parseMode: ParseMode.Html, cancellationToken: ct);
            progressMsgId = initMsg.MessageId;
        }
        catch { }

        var spamCount = 0;
        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await _bot.SendMessage(group.TelegramGroupId, caption,
                    parseMode: ParseMode.Html, cancellationToken: ct);
                group.LastPostedAt = DateTime.UtcNow;
                posted++;
                spamCount = 0;

                if (progressMsgId.HasValue && posted % progressEvery == 0)
                {
                    try
                    {
                        await _bot.EditMessageText(user.TelegramId, progressMsgId.Value,
                            $"📤 <b>E'lon tarqatilmoqda (bot orqali)...</b>\n" +
                            $"✅ {posted}/{total} guruhga yuborildi",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                    }
                    catch { }
                }

                await Task.Delay(Math.Min(delayMs, 2000), ct);
            }
            catch (ApiRequestException apiEx)
            {
                if (apiEx.ErrorCode is 403 or 400)
                {
                    group.IsActive = false;
                    _logger.LogInformation("Group {Id} deactivated (bot removed/blocked)", group.TelegramGroupId);
                }
                else if (apiEx.Message.Contains("Too Many Requests") || apiEx.ErrorCode == 429)
                {
                    spamCount++;
                    if (spamCount >= 3)
                    {
                        await NotifySpamDetectedAsync(user, spamCount, ct);
                        await Task.Delay(30_000, ct);
                        spamCount = 0;
                    }
                    else await Task.Delay(5_000, ct);
                }
                else _logger.LogDebug(apiEx, "Group {GroupId} post failed", group.TelegramGroupId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Group {GroupId} post failed", group.TelegramGroupId);
            }
        }

        if (posted > 0) await _db.SaveChangesAsync(ct);

        if (progressMsgId.HasValue)
        {
            try
            {
                await _bot.EditMessageText(user.TelegramId, progressMsgId.Value,
                    $"✅ <b>E'lon muvaffaqiyatli tarqatildi!</b>\n" +
                    $"📤 {posted}/{total} guruhga yuborildi\n" +
                    $"#TruckBor #{post.FromCity?.Replace(" ", "")} #{post.ToCity?.Replace(" ", "")}",
                    parseMode: ParseMode.Html, cancellationToken: ct);
            }
            catch { }
        }

        _logger.LogInformation("Post {PostId} sent to {Count}/{Total} groups via bot API",
            post.Id, posted, total);
    }

    // ═══ CHANNEL POSTING ═════════════════════════════════════════════════════
    public async Task PostToChannelAsync(long postId, CancellationToken ct = default)
    {
        var post = await _db.Posts
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == postId, ct);
        if (post is null) return;

        var channelSetting = await _db.Settings
            .FirstOrDefaultAsync(x => x.Key == "post_channel_id", ct);
        if (channelSetting is null ||
            !long.TryParse(channelSetting.Value, out var channelId) ||
            channelId == 0) return;

        var caption = BuildCaption(post, post.User);
        try
        {
            await _bot.SendMessage(channelId, caption,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Channel post failed for post {PostId}", postId);
        }
    }

    // ═══ SPAM HANDLING ═══════════════════════════════════════════════════════
    private async Task NotifySpamDetectedAsync(
        Domain.Entities.User user, int count, CancellationToken ct)
    {
        try
        {
            await _bot.SendMessage(user.TelegramId,
                $"⚠️ <b>Spam aniqlandi!</b>\n\n" +
                $"📊 {count} ta guruhda xatolik yuz berdi.\n" +
                "⏳ 30 soniya kutilmoqda...\n\n" +
                "ℹ️ @SpamBot ga murojaat qilib blokdan chiqishingiz mumkin.",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch { }
    }

    public async Task HandleSpamAsync(long telegramAccountId, CancellationToken ct = default)
    {
        var account = await _db.TelegramAccounts
            .FirstOrDefaultAsync(x => x.Id == telegramAccountId, ct);
        if (account is null) return;

        account.IsSpammed = true;
        account.SpammedAt = DateTime.UtcNow;
        account.IsActive = false;
        await _db.SaveChangesAsync(ct);

        // Auto-fix spam if session exists
        if (!string.IsNullOrEmpty(account.SessionString))
        {
            var fix = await _pyro.FixSpamAsync(account.SessionString, ct);
            _logger.LogInformation("Auto spam fix for {Phone}: {Result}",
                account.PhoneNumber, fix.Success ? "OK" : fix.Error);
        }

        _logger.LogWarning("TelegramAccount {Phone} marked as spammed", account.PhoneNumber);
    }

    // ═══ CAPTION BUILDER ═════════════════════════════════════════════════════
    private static string BuildCaption(Domain.Entities.Post post, Domain.Entities.User? user)
    {
        var roleIcon = post.PostedBy switch
        {
            UserRole.Driver     => "🚛",
            UserRole.CargoOwner => "📦",
            _                   => "🧭"
        };

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); }
        catch { tz = TimeZoneInfo.Utc; }
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        return
            $"{roleIcon} <b>{post.FromCity} → {post.ToCity}</b>\n" +
            (string.IsNullOrEmpty(post.CargoType) ? "" : $"🏷 {post.CargoType}") +
            (string.IsNullOrEmpty(post.Weight)    ? "\n" : $"  ⚖️ {post.Weight}\n") +
            (string.IsNullOrEmpty(post.Price)     ? "" : $"💰 {post.Price}\n") +
            $"📞 <code>{post.ContactPhone}</code>\n" +
            (user is not null ? $"👤 {user.FullName}\n" : "") +
            $"🕐 {now:dd.MM.yyyy HH:mm}\n" +
            $"#TruckBor #{post.FromCity?.Replace(" ", "")} #{post.ToCity?.Replace(" ", "")}\n" +
            "━━━━━━━━━━━━━━━━━━━━━\n" +
            "🤖 TruckBor Bot orqali yuborildi";
    }
}
