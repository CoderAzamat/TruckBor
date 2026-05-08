using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

public class Subscription : BaseEntity
{
    public long UserId { get; set; }
    public long TariffId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; }

    public int DaysLeft => (int)(EndDate - DateTime.UtcNow).TotalDays;

    // Navigation
    public User? User { get; set; }
    public Tariff? Tariff { get; set; }
}