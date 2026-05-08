using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
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

        var tariff = sub.Tariff;
        var caption = BuildCaption(post, user);

        var groups = await _db.Groups
            .Where(x => x.IsActive && x.MinTariffLevel <= tariff.SortOrder)
            .OrderBy(x => x.LastPostedAt ?? DateTime.MinValue)
            .Take(tariff.MaxGroups)
            .ToListAsync(ct);

        if (!groups.Any()) return;

        var delayMs = Math.Max(tariff.PostIntervalMinutes * 60_000 / Math.Max(groups.Count, 1), 500);
        var posted = 0;

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await _bot.SendMessage(group.TelegramGroupId, caption,
                    parseMode: ParseMode.Html, cancellationToken: ct);
                group.LastPostedAt = DateTime.UtcNow;
                posted++;
                await Task.Delay(Math.Min(delayMs, 2000), ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Group {GroupId} post failed", group.TelegramGroupId);
            }
        }

        if (posted > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Post {PostId} sent to {Count}/{Total} groups",
            postId, posted, groups.Count);
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
