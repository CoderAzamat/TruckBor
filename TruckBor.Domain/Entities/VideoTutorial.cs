using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

/// <summary>
/// Admin paneldagi video darsliklar — har bir xizmat uchun.
/// </summary>
public class VideoTutorial : BaseEntity
{
    /// <summary>Sarlavha (uz)</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Sarlavha (ru)</summary>
    public string? TitleRu { get; set; }

    /// <summary>Xizmat kaliti: "tariff", "virtual_number", "premium", "payment", "general"</summary>
    public string ServiceKey { get; set; } = "general";

    /// <summary>YouTube URL yoki to'liq URL</summary>
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>Telegram video file_id (agar yuklab olingan bo'lsa)</summary>
    public string? TelegramFileId { get; set; }

    /// <summary>Qisqacha izoh</summary>
    public string? Description { get; set; }

    /// <summary>Ko'rsatilsinmi?</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Tartib</summary>
    public int SortOrder { get; set; }
}
