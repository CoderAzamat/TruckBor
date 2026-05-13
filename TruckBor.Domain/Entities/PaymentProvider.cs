using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

/// <summary>
/// To'lov tizimi (Click, Payme, Uzum, Telegram Stars, Manual Card).
/// Admin paneldan yoqish/o'chirish mumkin, kod o'zgartirmasdan.
/// </summary>
public class PaymentProvider : BaseEntity
{
    /// <summary>Kod: "click", "payme", "uzum", "stars", "card"</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Ko'rinadigan nom: "Click", "Payme", "Uzum Bank"</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Emoji ikonka</summary>
    public string IconEmoji { get; set; } = "💳";

    /// <summary>To'lov turi</summary>
    public PaymentType Type { get; set; } = PaymentType.Manual;

    /// <summary>Faol holati — admin o'zgartiradi</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Merchant ID (Click/Payme/Uzum uchun)</summary>
    public string? MerchantId { get; set; }

    /// <summary>Service ID (Click uchun)</summary>
    public string? ServiceId { get; set; }

    /// <summary>API maxfiy kaliti</summary>
    public string? SecretKey { get; set; }

    /// <summary>API kaliti</summary>
    public string? ApiKey { get; set; }

    /// <summary>Webhook maxfiy kaliti (imzoni tekshirish uchun)</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Minimal to'lov summa (so'mda)</summary>
    public decimal MinAmount { get; set; } = 1_000;

    /// <summary>Maksimal to'lov summa (so'mda)</summary>
    public decimal MaxAmount { get; set; } = 50_000_000;

    /// <summary>Komissiya foizi (%)</summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>Qo'shimcha JSON konfiguratsiya</summary>
    public string? AdditionalConfig { get; set; }

    /// <summary>Tartiblash raqami</summary>
    public int SortOrder { get; set; }
}
