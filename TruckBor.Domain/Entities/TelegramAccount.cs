using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class TelegramAccount : BaseEntity
{
    public long UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? SessionString { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; }
    public bool IsSpammed { get; set; }
    public DateTime? SpammedAt { get; set; }
    public DateTime? LastUsed { get; set; }
    public int PostsSent { get; set; }

    // Navigation
    public User? User { get; set; }
}