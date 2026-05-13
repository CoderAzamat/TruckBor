using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

public class Payment : BaseEntity
{
    public long UserId { get; set; }
    public long? TariffId { get; set; }
    public decimal Amount { get; set; }
    public PaymentType Type { get; set; } = PaymentType.Manual;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Chek rasmi (Telegram file_id)</summary>
    public string? CheckFileId { get; set; }

    /// <summary>To'lov provayder kodi: "click", "payme", "uzum", "stars", "card"</summary>
    public string? ProviderCode { get; set; }

    /// <summary>Provayder tomonidan berilgan tranzaksiya ID</summary>
    public string? TransactionId { get; set; }

    /// <summary>Izoh (foydalanuvchi yoki admin)</summary>
    public string? Comment { get; set; }

    /// <summary>Rad etish sababi</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Telegram Stars miqdori (Stars to'lovida)</summary>
    public int? StarsAmount { get; set; }

    public long? ApprovedByAdminId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public Tariff? Tariff { get; set; }
    public ICollection<BalanceTransaction> BalanceTransactions { get; set; } = new List<BalanceTransaction>();
}