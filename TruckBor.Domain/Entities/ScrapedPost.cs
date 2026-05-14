using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

/// <summary>
/// E'lon guruhlardan scrape qilingan (tizim akkaunt orqali).
/// AI tahlil qilib FromCity, ToCity, Price va boshqa maydonlarni to'ldiradi.
/// </summary>
public class ScrapedPost : BaseEntity
{
    /// <summary>Qaysi guruhdan olingan</summary>
    public long SourceGroupId { get; set; }
    public string? SourceGroupTitle { get; set; }

    /// <summary>Telegram xabar IDsi (dublikatni oldini olish uchun)</summary>
    public long TelegramMessageId { get; set; }

    /// <summary>Xabarning asl matni</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>Xabar yozgan odamning Telegram IDsi</summary>
    public long? AuthorTelegramId { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>Xabar vaqti (Telegramdagi)</summary>
    public DateTime MessageDate { get; set; }

    // ── AI tahlil natijalari ─────────────────────────────────
    public bool IsProcessed { get; set; }
    public PostType PostType { get; set; } = PostType.Cargo;
    public string? FromCity { get; set; }
    public string? ToCity { get; set; }
    public string? CargoType { get; set; }
    public string? Weight { get; set; }
    public string? VehicleType { get; set; }
    public string? Price { get; set; }
    public string? ContactPhone { get; set; }
    public string? Description { get; set; }

    /// <summary>AI ishonch darajasi (0-100)</summary>
    public int Confidence { get; set; }

    /// <summary>E'lon sifatida tan olinganmi (AI tomonidan)</summary>
    public bool IsRelevant { get; set; }

    /// <summary>Mini App da ko'rinadimi</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Necha marta ko'rildi</summary>
    public int ViewCount { get; set; }

    /// <summary>Kontakt ko'rishlar soni</summary>
    public int ContactViews { get; set; }

    /// <summary>Muddati tugash vaqti</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(3);
}
