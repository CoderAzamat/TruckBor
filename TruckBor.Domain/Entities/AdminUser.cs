using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class AdminUser : BaseEntity
{
    public long TelegramId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsSuper { get; set; }

    // Web panel login
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Ruxsatlar
    public bool CanManageUsers { get; set; } = true;
    public bool CanManagePayments { get; set; } = true;
    public bool CanManageTariffs { get; set; }
    public bool CanManageGroups { get; set; }
    public bool CanManageCards { get; set; }
    public bool CanManageChannels { get; set; }
    public bool CanBroadcast { get; set; }
    public bool CanViewStatistics { get; set; } = true;
    public bool CanManageAdmins { get; set; }
    public bool CanManageSettings { get; set; }
    public bool CanManageVirtual { get; set; }
    public bool CanManagePremium { get; set; }
    public bool CanManageVideos { get; set; }
}