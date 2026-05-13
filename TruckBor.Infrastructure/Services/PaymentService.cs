using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Telegram.Keyboards;

namespace TruckBor.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IAppDbContext _db;
    private readonly ITelegramService _tg;
    private readonly ILocalizationService _loc;
    private readonly IBalanceService _balance;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IAppDbContext db,
        ITelegramService tg,
        ILocalizationService loc,
        IBalanceService balance,
        ILogger<PaymentService> logger)
    {
        _db = db; _tg = tg; _loc = loc; _balance = balance; _logger = logger;
    }

    public async Task<bool> ApproveAsync(long paymentId, long adminId, decimal amount, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(x => x.Tariff)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending) return false;

        payment.Amount            = amount;
        payment.Status            = PaymentStatus.Approved;
        payment.ApprovedByAdminId = adminId;
        payment.ApprovedAt        = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var user = payment.User ?? await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user is null) return true;

        // Balansni to'ldirish
        var desc = payment.Tariff is not null
            ? $"Tarif to'lovi: {payment.Tariff.Name}"
            : "Balans to'ldirish";

        await _balance.CreditAsync(user.Id, amount, "topup", desc, paymentId, adminId, ct);

        // Agar tarif to'lovi bo'lsa — obunani faollashtirish
        if (payment.Tariff is not null)
        {
            var existingSub = await _db.Subscriptions
                .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                    x.Status == SubscriptionStatus.Active &&
                    x.EndDate > DateTime.UtcNow, ct);

            if (existingSub is not null)
                existingSub.EndDate = existingSub.EndDate.AddDays(payment.Tariff.DurationDays);
            else
                _db.Subscriptions.Add(new Domain.Entities.Subscription
                {
                    UserId    = user.Id,
                    TariffId  = payment.Tariff.Id,
                    Status    = SubscriptionStatus.Active,
                    StartDate = DateTime.UtcNow,
                    EndDate   = DateTime.UtcNow.AddDays(payment.Tariff.DurationDays),
                });

            await _db.SaveChangesAsync(ct);

            // Foydalanuvchiga xabar
            var userMsg =
                $"✅ <b>To'lov tasdiqlandi!</b>\n\n" +
                $"💰 Miqdor: <b>{amount:N0}</b> so'm\n" +
                $"⭐ <b>{payment.Tariff.Name}</b> tarifi faollashtirildi!\n" +
                $"📅 {payment.Tariff.DurationDays} kun davomida foydalanishingiz mumkin.\n\n" +
                $"💳 Joriy balans: <b>{user.Balance:N0}</b> so'm";

            try { await _tg.SendMessageAsync(user.TelegramId, userMsg, Keyboards.MainMenu(user.Language), ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
        }
        else
        {
            // Oddiy balans to'ldirish
            var userMsg =
                $"✅ <b>Balans to'ldirildi!</b>\n\n" +
                $"💰 Qo'shildi: <b>{amount:N0}</b> so'm\n" +
                $"💳 Joriy balans: <b>{user.Balance:N0}</b> so'm\n\n" +
                "Istalgan xizmatni balansdan sotib olishingiz mumkin! 🚀";

            try { await _tg.SendMessageAsync(user.TelegramId, userMsg, Keyboards.MainMenu(user.Language), ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
        }

        return true;
    }

    public async Task<bool> RejectAsync(long paymentId, long adminId, string reason, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null) return false;

        payment.Status            = PaymentStatus.Rejected;
        payment.RejectionReason   = reason;
        payment.ApprovedByAdminId = adminId;
        await _db.SaveChangesAsync(ct);

        var user = payment.User ?? await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user is not null)
        {
            try
            {
                await _tg.SendMessageAsync(user.TelegramId,
                    $"❌ <b>To'lovingiz rad etildi.</b>\n\n" +
                    $"Sabab: {reason}\n\n📞 Savol bo'lsa: @TruckBorAdmin", ct: ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
        }

        return true;
    }

    /// <summary>Balansdan tarif xarid qilish (bot ichida).</summary>
    public async Task<(bool Success, string Error)> BuyTariffFromBalanceAsync(long userId, long tariffId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return (false, "Foydalanuvchi topilmadi");

        var tariff = await _db.Tariffs.FirstOrDefaultAsync(x => x.Id == tariffId && x.IsActive, ct);
        if (tariff is null) return (false, "Tarif topilmadi");

        var price = tariff.DiscountPrice ?? tariff.Price;
        if (user.Balance < price)
            return (false, $"Balansingiz yetarli emas.\n💰 Kerak: {price:N0} so'm\n💳 Balans: {user.Balance:N0} so'm");

        var deducted = await _balance.DebitAsync(user.Id, price, "tariff_buy",
            $"Tarif: {tariff.Name} ({tariff.DurationDays} kun)", ct: ct);
        if (!deducted) return (false, "Tranzaksiya amalga oshmadi");

        // Obuna yaratish
        var existing = await _db.Subscriptions
            .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                x.Status == SubscriptionStatus.Active &&
                x.EndDate > DateTime.UtcNow, ct);

        if (existing is not null)
            existing.EndDate = existing.EndDate.AddDays(tariff.DurationDays);
        else
            _db.Subscriptions.Add(new Domain.Entities.Subscription
            {
                UserId    = user.Id,
                TariffId  = tariff.Id,
                Status    = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate   = DateTime.UtcNow.AddDays(tariff.DurationDays),
            });

        await _db.SaveChangesAsync(ct);
        return (true, string.Empty);
    }
}
