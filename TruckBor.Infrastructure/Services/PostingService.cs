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
    private readonly ILogger<PostingService> _logger;

    public PostingService(
        IAppDbContext db,
        ITelegramBotClient bot,
        ILogger<PostingService> logger)
    {
        _db = db; _bot = bot; _logger = logger;
    }

    public async Task PostToGroupsAsync(long postId, long userId, CancellationToken ct = default)
    {
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

        var total    = groups.Count;
        var delayMs  = Math.Max(tariff.PostIntervalMinutes * 60_000 / Math.Max(total, 1), 500);
        var posted   = 0;

        // ── Progress message ─────────────────────────────────────────────
        int? progressMsgId = null;
        var  progressEvery = Math.Max(total / 5, 1); // update ~5 times total

        try
        {
            var initMsg = await _bot.SendMessage(
                user.TelegramId,
                $"📤 <b>E'lon tarqatilmoqda...</b>\n" +
                $"⏳ 0/{total} guruhga yuborildi",
                parseMode: ParseMode.Html, cancellationToken: ct);
            progressMsgId = initMsg.MessageId;
        }
        catch { /* progress is non-critical */ }

        // ── Posting loop ─────────────────────────────────────────────────
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
                spamCount = 0; // reset on success

                // Update progress
                if (progressMsgId.HasValue && posted % progressEvery == 0)
                {
                    try
                    {
                        await _bot.EditMessageText(
                            user.TelegramId, progressMsgId.Value,
                            $"📤 <b>E'lon tarqatilmoqda...</b>\n" +
                            $"✅ {posted}/{total} guruhga yuborildi",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                    }
                    catch { }
                }

                await Task.Delay(Math.Min(delayMs, 2000), ct);
            }
            catch (ApiRequestException apiEx)
            {
                // Kicked/blocked from group — deactivate it
                if (apiEx.ErrorCode is 403 or 400)
                {
                    group.IsActive = false;
                    _logger.LogInformation("Group {Id} deactivated (bot removed/blocked)", group.TelegramGroupId);
                }
                else if (apiEx.Message.Contains("Too Many Requests") || apiEx.ErrorCode == 429)
                {
                    spamCount++;
                    _logger.LogWarning("Spam/flood on group {Id}, count={C}", group.TelegramGroupId, spamCount);
                    if (spamCount >= 3)
                    {
                        // Notify admin about spam detection
                        await NotifySpamDetectedAsync(user, spamCount, ct);
                        await Task.Delay(30_000, ct); // wait 30s after flood
                        spamCount = 0;
                    }
                    else
                    {
                        await Task.Delay(5_000, ct);
                    }
                }
                else
                {
                    _logger.LogDebug(apiEx, "Group {GroupId} post failed", group.TelegramGroupId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Group {GroupId} post failed", group.TelegramGroupId);
            }
        }

        if (posted > 0)
            await _db.SaveChangesAsync(ct);

        // ── Final status ─────────────────────────────────────────────────
        if (progressMsgId.HasValue)
        {
            try
            {
                await _bot.EditMessageText(
                    user.TelegramId, progressMsgId.Value,
                    $"✅ <b>E'lon muvaffaqiyatli tarqatildi!</b>\n" +
                    $"📤 {posted}/{total} guruhga yuborildi\n" +
                    $"#TruckBor #{post.FromCity?.Replace(" ", "")} #{post.ToCity?.Replace(" ", "")}",
                    parseMode: ParseMode.Html, cancellationToken: ct);
            }
            catch { }
        }

        _logger.LogInformation("Post {PostId} sent to {Count}/{Total} groups",
            postId, posted, total);
    }

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

    private async Task NotifySpamDetectedAsync(
        Domain.Entities.User user, int count, CancellationToken ct)
    {
        try
        {
            await _bot.SendMessage(
                user.TelegramId,
                $"⚠️ <b>Spam aniqlandi!</b>\n\n" +
                $"📊 {count} ta guruhda xatolik yuz berdi.\n" +
                "⏳ 30 soniya kutilmoqda...\n\n" +
                "ℹ️ @SpamBot ga murojaat qilib blokdan chiqishingiz mumkin.",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
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

        _logger.LogWarning("TelegramAccount {Phone} marked as spammed", account.PhoneNumber);
    }

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
            $"#TruckBor #{post.FromCity?.Replace(" ", "")} #{post.ToCity?.Replace(" ", "")}";
    }
}
