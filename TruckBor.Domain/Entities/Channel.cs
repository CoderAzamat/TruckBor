using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class Channel : BaseEntity
{
    public long TelegramChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string InviteLink { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
}