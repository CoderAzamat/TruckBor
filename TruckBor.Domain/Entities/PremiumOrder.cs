using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

/// <summary>
/// Telegram Premium buyurtmasi.
/// </summary>
public class PremiumOrder : BaseEntity
{
    public long UserId { get; set; }

    /// <summary>Telegram username (Premium berilgan akkaunt)</summary>
    public string? TargetUsername { get; set; }

    /// <summary>Davomiyligi (oy): 1, 3, 6, 12</summary>
    public int MonthCount { get; set; } = 1;

    /// <summary>To'langan summa</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>To'lov turi: "balance", "stars"</summary>
    public string PaymentMethod { get; set; } = "balance";

    /// <summary>Holat: "pending", "processing", "completed", "rejected"</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Admin izohi / rad etish sababi</summary>
    public string? AdminNote { get; set; }

    /// <summary>Bajardi (admin TelegramId)</summary>
    public long? ProcessedByAdminId { get; set; }

    /// <summary>Bajarilgan vaqt</summary>
    public DateTime? ProcessedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
