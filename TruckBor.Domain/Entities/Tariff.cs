using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class Tariff : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int DurationDays { get; set; }
    public int MaxAccounts { get; set; }
    public int MaxGroups { get; set; }
    public int PostsPerDay { get; set; }
    public int PostIntervalMinutes { get; set; } = 30;
    public bool IsRecommended { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}