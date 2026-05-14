using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Services;

/// <summary>
/// Tizim akkaunt orqali guruhlardan e'lonlarni scrape qiladi,
/// AI bilan tahlil qilib ScrapedPost jadvaliga saqlaydi.
/// </summary>
public class ScrapingService
{
    private readonly IAppDbContext _db;
    private readonly ITelegramSmsAuthService _pyro;
    private readonly ILogger<ScrapingService> _logger;

    public ScrapingService(
        IAppDbContext db,
        ITelegramSmsAuthService pyro,
        ILogger<ScrapingService> logger)
    {
        _db = db; _pyro = pyro; _logger = logger;
    }

    /// <summary>
    /// Barcha faol tizim akkauntlari orqali guruhlardan e'lonlarni yig'adi.
    /// </summary>
    public async Task ScrapeAllGroupsAsync(CancellationToken ct)
    {
        var account = await _db.SystemAccounts
            .Where(x => x.IsActive && !x.IsSpammed &&
                        x.SessionString != null && x.SessionString != "")
            .OrderBy(x => x.LastScrapeAt ?? DateTime.MinValue)
            .FirstOrDefaultAsync(ct);

        if (account is null)
        {
            _logger.LogDebug("No active system account for scraping");
            return;
        }

        var groups = await _db.Groups
            .Where(x => x.IsActive)
            .OrderBy(x => x.LastPostedAt ?? DateTime.MinValue)
            .Take(50) // har safar 50 ta guruhdan
            .ToListAsync(ct);

        if (!groups.Any()) return;

        var totalScraped = 0;
        var totalNew = 0;

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var result = await _pyro.ScrapeGroupAsync(
                    account.SessionString!, group.TelegramGroupId, limit: 30, ct);

                if (!result.Success || result.Messages is null) continue;

                foreach (var msg in result.Messages)
                {
                    // Dublikat tekshirish
                    var exists = await _db.ScrapedPosts
                        .AnyAsync(x => x.SourceGroupId == group.TelegramGroupId &&
                                       x.TelegramMessageId == msg.Id, ct);
                    if (exists) continue;

                    // Logistika e'loniga o'xshashmi? Oddiy tekshirish
                    if (!IsLikelyLogisticsPost(msg.Text)) continue;

                    var scraped = new ScrapedPost
                    {
                        SourceGroupId = group.TelegramGroupId,
                        SourceGroupTitle = group.Title,
                        TelegramMessageId = msg.Id,
                        RawText = msg.Text,
                        AuthorTelegramId = msg.FromId,
                        AuthorName = msg.FromName,
                        MessageDate = msg.Date,
                        IsProcessed = false,
                        IsRelevant = true, // dastlab true, AI keyin aniqlashtiradi
                        ExpiresAt = DateTime.UtcNow.AddDays(3),
                    };

                    // Oddiy AI tahlil (regex-based)
                    ParsePostFields(scraped);

                    _db.ScrapedPosts.Add(scraped);
                    totalNew++;
                }

                totalScraped += result.Messages.Count;
                group.LastPostedAt = DateTime.UtcNow; // scrape qilinganini belgilash

                await Task.Delay(1000, ct); // rate limit
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Scrape failed for group {GroupId}", group.TelegramGroupId);
            }
        }

        account.LastScrapeAt = DateTime.UtcNow;
        account.TotalScraped += totalScraped;
        account.LastUsed = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Scraping complete: {Groups} groups, {Total} messages, {New} new posts",
            groups.Count, totalScraped, totalNew);
    }

    /// <summary>
    /// Muddati o'tgan scraped postlarni o'chiradi.
    /// </summary>
    public async Task CleanupExpiredAsync(CancellationToken ct)
    {
        var expired = await _db.ScrapedPosts
            .Where(x => x.ExpiresAt < DateTime.UtcNow)
            .Take(500)
            .ToListAsync(ct);

        if (!expired.Any()) return;

        foreach (var p in expired)
            _db.ScrapedPosts.Remove(p);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Cleaned up {Count} expired scraped posts", expired.Count);
    }

    // ── Oddiy logistika e'loni tekshirish ────────────────────────────────
    private static bool IsLikelyLogisticsPost(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 15) return false;

        // Logistika kalit so'zlari
        var keywords = new[]
        {
            "yuk", "груз", "mashina", "машина", "avto", "fura",
            "рейс", "рефрижератор", "тент", "бортовой",
            "dogruz", "догруз", "bo'sh", "бош", "свободен",
            "kerak", "нужен", "ищу", "qidiryapman",
            "tonna", "тонн", "kg", "кг",
            "➡", "→", "—", "📦", "🚛", "🚚",
            "toshkent", "samarqand", "buxoro", "andijon", "farg'ona",
            "namangan", "navoiy", "qashqadaryo", "surxondaryo",
            "xorazm", "jizzax", "sirdaryo", "nukus",
            "ташкент", "самарканд", "бухара", "андижан", "фергана",
            "москва", "алмата", "алматы", "казахстан", "россия",
            "qozog'iston", "rossiya", "moskva", "almati",
        };

        var lower = text.ToLowerInvariant();
        var matchCount = keywords.Count(k => lower.Contains(k));
        return matchCount >= 2; // kamida 2 ta kalit so'z bo'lishi kerak
    }

    // ── Oddiy regex-based field parsing ─────────────────────────────────
    private static void ParsePostFields(ScrapedPost post)
    {
        var text = post.RawText;
        var lower = text.ToLowerInvariant();

        // Yo'nalish aniqlash (→, ➡, -, dan, ga)
        var cities = ExtractCities(text);
        if (cities.from is not null) post.FromCity = cities.from;
        if (cities.to is not null) post.ToCity = cities.to;

        // Telefon raqam
        var phoneMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:\+?998|8)\s*[\d\s\-()]{8,12}");
        if (phoneMatch.Success)
            post.ContactPhone = phoneMatch.Value.Trim();

        // Narx
        var priceMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d[\d\s,.]*)\s*(so'm|сум|sum|usd|\$|USD|у\.е|dollar)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (priceMatch.Success)
            post.Price = priceMatch.Value.Trim();

        // Vazn
        var weightMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d+[\d.,]*)\s*(tonn|тонн|kg|кг|t\b)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (weightMatch.Success)
            post.Weight = weightMatch.Value.Trim();

        // Post turi (yuk yoki mashina)
        if (lower.Contains("bo'sh") || lower.Contains("бош") || lower.Contains("свобод") ||
            lower.Contains("mashina") || lower.Contains("машина") || lower.Contains("fura"))
            post.PostType = PostType.Transport;
        else if (lower.Contains("dogruz") || lower.Contains("догруз"))
            post.PostType = PostType.Dogruz;
        else
            post.PostType = PostType.Cargo;

        post.IsProcessed = true;
        post.Confidence = (post.FromCity is not null && post.ToCity is not null) ? 80 : 40;
    }

    private static (string? from, string? to) ExtractCities(string text)
    {
        // "Toshkent → Samarqand", "Toshkent - Samarqand", "Toshkentdan Samarqandga"
        var arrowMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"([A-ZА-ЯЁa-zа-яёA-Za-z'ʻ]{3,20})\s*[→➡\-–—]\s*([A-ZА-ЯЁa-zа-яёA-Za-z'ʻ]{3,20})");
        if (arrowMatch.Success)
            return (arrowMatch.Groups[1].Value.Trim(), arrowMatch.Groups[2].Value.Trim());

        // "...dan ...ga" pattern
        var danGaMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"([A-ZА-ЯЁa-zа-яёA-Za-z'ʻ]{3,20})(?:dan|дан)\s+([A-ZА-ЯЁa-zа-яёA-Za-z'ʻ]{3,20})(?:ga|га)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (danGaMatch.Success)
            return (danGaMatch.Groups[1].Value.Trim(), danGaMatch.Groups[2].Value.Trim());

        return (null, null);
    }
}
