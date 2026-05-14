using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

/// <summary>
/// Admin tomonidan boshqariladigan tizim Telegram akkaunt.
/// Guruhlardan e'lon scrape qilish, guruh qidirish va qo'shilish uchun ishlatiladi.
/// Foydalanuvchi akkauntlaridan farqli - bu botning o'zi uchun.
/// </summary>
public class SystemAccount : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? SessionString { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; }
    public bool IsSpammed { get; set; }
    public DateTime? SpammedAt { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime? LastScrapeAt { get; set; }

    /// <summary>Qancha guruhga qo'shilgan</summary>
    public int JoinedGroupsCount { get; set; }

    /// <summary>Jami scrape qilingan xabarlar</summary>
    public int TotalScraped { get; set; }

    /// <summary>Eslatma / izoh</summary>
    public string? Note { get; set; }

    /// <summary>Akkaunt maqsadi: scrape / join / all</summary>
    public string Purpose { get; set; } = "all";
}
