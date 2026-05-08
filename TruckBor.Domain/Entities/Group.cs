using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class Group : BaseEntity
{
    public long TelegramGroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? InviteLink { get; set; }
    public int MembersCount { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public int MinTariffLevel { get; set; } = 0;
    public DateTime? LastPostedAt { get; set; }
}