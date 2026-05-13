namespace TruckBor.Application.Interfaces;

public interface IBalanceService
{
    /// <summary>Foydalanuvchi balansini ko'paytirish va tarix saqlash.</summary>
    Task<bool> CreditAsync(long userId, decimal amount, string reasonCode, string? description = null, long? paymentId = null, long? adminId = null, CancellationToken ct = default);

    /// <summary>Foydalanuvchi balansidan yechish. Yetarli bo'lmasa false qaytaradi.</summary>
    Task<bool> DebitAsync(long userId, decimal amount, string reasonCode, string? description = null, long? paymentId = null, long? adminId = null, CancellationToken ct = default);

    /// <summary>Balans tarixini olish (sahifalanib).</summary>
    Task<List<BalanceHistoryDto>> GetHistoryAsync(long userId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>Joriy balansni olish.</summary>
    Task<decimal> GetBalanceAsync(long userId, CancellationToken ct = default);
}

public record BalanceHistoryDto(
    long Id,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string ReasonCode,
    string? Description,
    DateTime CreatedAt
);
