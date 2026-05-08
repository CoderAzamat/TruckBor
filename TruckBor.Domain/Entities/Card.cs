using TruckBor.Domain.Common;

namespace TruckBor.Domain.Entities;

public class Card : BaseEntity
{
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolder { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}