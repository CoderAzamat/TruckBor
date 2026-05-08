namespace TruckBor.Application.Interfaces;

public interface IPaymentService
{
    Task<bool> ApproveAsync(long paymentId, long adminId, decimal amount, CancellationToken ct = default);
    Task<bool> RejectAsync(long paymentId, long adminId, string reason, CancellationToken ct = default);
}