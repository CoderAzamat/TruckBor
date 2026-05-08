using TruckBor.Domain.Common;
using TruckBor.Domain.Enums;

namespace TruckBor.Domain.Entities;

public class Post : BaseEntity
{
    public long UserId { get; set; }
    public PostType PostType { get; set; } = PostType.Cargo;
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    public double? FromLat { get; set; }
    public double? FromLng { get; set; }
    public double? ToLat { get; set; }
    public double? ToLng { get; set; }
    public string? CargoType { get; set; }
    public string? Weight { get; set; }
    public string? VehicleType { get; set; }
    public string? Price { get; set; }
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public UserRole PostedBy { get; set; }
    public PostStatus Status { get; set; } = PostStatus.Active;
    public bool IsVerified { get; set; }
    public bool IsFromGroup { get; set; }
    public string? GroupSource { get; set; }
    public int ViewCount { get; set; }
    public int ContactViews { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    // Navigation
    public User? User { get; set; }
}
