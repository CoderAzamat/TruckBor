using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

/// <summary>
/// Virtual raqam buyurtmasi (SMS-Activate orqali).
/// </summary>
public class VirtualNumberOrder : BaseEntity
{
    public long UserId { get; set; }

    /// <summary>SMS-Activate activation ID</summary>
    public string? ActivationId { get; set; }

    /// <summary>Berilgan telefon raqam</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Mamlakat kodi: "uz", "ru", "kz"</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Xizmat: "tg" (Telegram), "wa" (WhatsApp), "gg" (Google)</summary>
    public string Service { get; set; } = "tg";

    /// <summary>Holat: "pending", "received", "expired", "cancelled", "done"</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Olingan SMS kodi</summary>
    public string? SmsCode { get; set; }

    /// <summary>To'langan summa (so'mda)</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Amal qilish muddati</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>SMS qabul qilingan vaqt</summary>
    public DateTime? SmsReceivedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
