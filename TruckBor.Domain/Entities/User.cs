using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

public class User : BaseEntity
{
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public Language Language { get; set; } = Language.UzLatin;
    public UserRole Role { get; set; } = UserRole.Logist;
    public bool IsOnboarded { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsPremium { get; set; }
    public decimal Balance { get; set; }
    public int TotalPosts { get; set; }
    public DateTime? LastActivity { get; set; }

    // Navigation
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<TelegramAccount> TelegramAccounts { get; set; } = new List<TelegramAccount>();
}