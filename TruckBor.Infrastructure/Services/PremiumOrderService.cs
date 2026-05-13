using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Services;

public class PremiumOrderService : IPremiumOrderService
{
    private readonly IAppDbContext _db;
    private readonly IBalanceService _balance;
    private readonly ITelegramService _tg;
    private readonly ILogger<PremiumOrderService> _logger;

    private static readonly Dictionary<int, string> MonthKeys = new()
    {
        [1] = "premium_1month_price",
        [3] = "premium_3month_price",
        [6] = "premium_6month_price",
        [12] = "premium_12month_price",
    };

    public PremiumOrderService(IAppDbContext db, IBalanceService balance, ITelegramService tg, ILogger<PremiumOrderService> logger)
    {
        _db = db; _balance = balance; _tg = tg; _logger = logger;
    }

    private async Task<decimal> GetPriceAsync(int months, CancellationToken ct)
    {
        var key = MonthKeys.GetValueOrDefault(months, "premium_1month_price");
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
        return decimal.TryParse(setting?.Value, out var p) ? p : 99_000;
    }

    public async Task<PremiumOrderResult> CreateOrderAsync(long userId, string targetUsername, int monthCount, string paymentMethod, CancellationToken ct = default)
    {
        var price = await GetPriceAsync(monthCount, ct);

        if (paymentMethod == "balance")
        {
            var deducted = await _balance.DebitAsync(userId, price, "premium_buy",
                $"Telegram Premium {monthCount} oy — @{targetUsername}", ct: ct);
            if (!deducted)
                return new PremiumOrderResult(false, "Balansingiz yetarli emas.", null);
        }

        var order = new PremiumOrder
        {
            UserId         = userId,
            TargetUsername = targetUsername.TrimStart('@'),
            MonthCount     = monthCount,
            AmountPaid     = price,
            PaymentMethod  = paymentMethod,
            Status         = "pending",
        };
        _db.PremiumOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Premium order created: user={UserId} target=@{Target} months={Months}", userId, targetUsername, monthCount);
        return new PremiumOrderResult(true, null, order.Id);
    }

    public async Task<bool> CompleteOrderAsync(long orderId, long adminId, string? note = null, CancellationToken ct = default)
    {
        var order = await _db.PremiumOrders.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null || order.Status != "pending") return false;

        order.Status             = "completed";
        order.ProcessedByAdminId = adminId;
        order.ProcessedAt        = DateTime.UtcNow;
        order.AdminNote          = note;
        await _db.SaveChangesAsync(ct);

        // Foydalanuvchiga xabar
        if (order.User is not null)
        {
            try
            {
                await _tg.SendMessageAsync(order.User.TelegramId,
                    $"✅ <b>Telegram Premium faollashtirildi!</b>\n\n" +
                    $"👤 Akkaunt: @{order.TargetUsername}\n" +
                    $"📅 Davomiyligi: {order.MonthCount} oy\n\n" +
                    "⭐ Premium imtiyozlaringizdan bahramand bo'ling!", ct: ct);
            }
            catch { }
        }
        return true;
    }

    public async Task<bool> RejectOrderAsync(long orderId, long adminId, string reason, CancellationToken ct = default)
    {
        var order = await _db.PremiumOrders.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null || order.Status != "pending") return false;

        // Balansni qaytarish
        if (order.PaymentMethod == "balance" && order.AmountPaid > 0)
        {
            await _balance.CreditAsync(order.UserId, order.AmountPaid, "refund",
                $"Premium buyurtma rad etildi: {reason}", ct: ct);
        }

        order.Status             = "rejected";
        order.ProcessedByAdminId = adminId;
        order.ProcessedAt        = DateTime.UtcNow;
        order.AdminNote          = reason;
        await _db.SaveChangesAsync(ct);

        if (order.User is not null)
        {
            try
            {
                await _tg.SendMessageAsync(order.User.TelegramId,
                    $"❌ <b>Telegram Premium buyurtmangiz rad etildi.</b>\n\n" +
                    $"Sabab: {reason}\n" +
                    (order.PaymentMethod == "balance" ? $"💰 {order.AmountPaid:N0} so'm qaytarildi.\n" : "") +
                    "\n📞 Savol bo'lsa: @TruckBorAdmin", ct: ct);
            }
            catch { }
        }
        return true;
    }

    public async Task<List<PremiumOrderDto>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        return await _db.PremiumOrders
            .Include(x => x.User)
            .Where(x => x.Status == "pending")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PremiumOrderDto(x.Id, x.UserId, x.User!.FullName, x.TargetUsername, x.MonthCount, x.AmountPaid, x.Status, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<List<PremiumOrderDto>> GetUserOrdersAsync(long userId, CancellationToken ct = default)
    {
        return await _db.PremiumOrders
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new PremiumOrderDto(x.Id, x.UserId, null, x.TargetUsername, x.MonthCount, x.AmountPaid, x.Status, x.CreatedAt))
            .ToListAsync(ct);
    }
}
