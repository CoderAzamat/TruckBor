namespace TruckBor.Application.Interfaces;

public interface IPremiumOrderService
{
    Task<PremiumOrderResult> CreateOrderAsync(long userId, string targetUsername, int monthCount, string paymentMethod, CancellationToken ct = default);
    Task<bool> CompleteOrderAsync(long orderId, long adminId, string? note = null, CancellationToken ct = default);
    Task<bool> RejectOrderAsync(long orderId, long adminId, string reason, CancellationToken ct = default);
    Task<List<PremiumOrderDto>> GetPendingOrdersAsync(CancellationToken ct = default);
    Task<List<PremiumOrderDto>> GetUserOrdersAsync(long userId, CancellationToken ct = default);
}

public record PremiumOrderResult(bool Success, string? Error, long? OrderId);
public record PremiumOrderDto(long Id, long UserId, string? UserName, string? TargetUsername, int MonthCount, decimal AmountPaid, string Status, DateTime CreatedAt);
