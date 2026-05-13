using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Services;

public class BalanceService : IBalanceService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<BalanceService> _logger;

    public BalanceService(IAppDbContext db, ILogger<BalanceService> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task<bool> CreditAsync(long userId, decimal amount, string reasonCode,
        string? description = null, long? paymentId = null, long? adminId = null,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return false;

        var before = user.Balance;
        user.Balance += amount;
        var after = user.Balance;

        _db.BalanceTransactions.Add(new BalanceTransaction
        {
            UserId        = userId,
            Amount        = amount,
            BalanceBefore = before,
            BalanceAfter  = after,
            ReasonCode    = reasonCode,
            Description   = description,
            PaymentId     = paymentId,
            PerformedBy   = adminId,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Balance credited: user={UserId} +{Amount} ({Reason})", userId, amount, reasonCode);
        return true;
    }

    public async Task<bool> DebitAsync(long userId, decimal amount, string reasonCode,
        string? description = null, long? paymentId = null, long? adminId = null,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return false;
        if (user.Balance < amount)
        {
            _logger.LogWarning("Insufficient balance: user={UserId} balance={Balance} required={Amount}", userId, user.Balance, amount);
            return false;
        }

        var before = user.Balance;
        user.Balance -= amount;
        var after = user.Balance;

        _db.BalanceTransactions.Add(new BalanceTransaction
        {
            UserId        = userId,
            Amount        = -amount,
            BalanceBefore = before,
            BalanceAfter  = after,
            ReasonCode    = reasonCode,
            Description   = description,
            PaymentId     = paymentId,
            PerformedBy   = adminId,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Balance debited: user={UserId} -{Amount} ({Reason})", userId, amount, reasonCode);
        return true;
    }

    public async Task<List<BalanceHistoryDto>> GetHistoryAsync(long userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return await _db.BalanceTransactions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BalanceHistoryDto(
                x.Id,
                x.Amount,
                x.BalanceBefore,
                x.BalanceAfter,
                x.ReasonCode,
                x.Description,
                x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        return user?.Balance ?? 0;
    }
}
