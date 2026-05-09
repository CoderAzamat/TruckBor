using TruckBor.Domain.Enums;

namespace TruckBor.Application.Interfaces;

public class AiExtractedPost
{
    public string? FromCity { get; set; }
    public string? ToCity { get; set; }
    public string? CargoType { get; set; }
    public string? Weight { get; set; }
    public string? VehicleType { get; set; }
    public string? Price { get; set; }
    public string? ContactPhone { get; set; }
    public PostType PostType { get; set; } = PostType.Cargo;
    public bool IsSuccessful { get; set; }
}

public interface IAiPostService
{
    Task<AiExtractedPost> ExtractPostFromTextAsync(string freeText, CancellationToken ct = default);
}
