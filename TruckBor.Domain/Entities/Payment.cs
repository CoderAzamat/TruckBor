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
    public string? CheckFileId { get; set; }
    public string? Comment { get; set; }
    public string? RejectionReason { get; set; }
    public long? ApprovedByAdminId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public Tariff? Tariff { get; set; }
}