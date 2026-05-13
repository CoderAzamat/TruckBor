using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

/// <summary>
/// Foydalanuvchi balansidagi har bir o'zgarish — to'liq tarix.
/// </summary>
public class BalanceTransaction : BaseEntity
{
    public long UserId { get; set; }

    /// <summary>O'zgarish miqdori: musbat = kirim, manfiy = chiqim</summary>
    public decimal Amount { get; set; }

    /// <summary>Tranzaksiya oldidan balans</summary>
    public decimal BalanceBefore { get; set; }

    /// <summary>Tranzaksiya keyin balans</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Sabab kodi: "topup", "tariff_buy", "virtual_number", "premium", "admin_gift", "refund"</summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Izoh (ko'rinadigan matn)</summary>
    public string? Description { get; set; }

    /// <summary>Bog'liq to'lov ID</summary>
    public long? PaymentId { get; set; }

    /// <summary>Kim amalga oshirdi: 0 = tizim, >0 = admin TelegramId</summary>
    public long? PerformedBy { get; set; }

    // Navigation
    public User? User { get; set; }
    public Payment? Payment { get; set; }
}
