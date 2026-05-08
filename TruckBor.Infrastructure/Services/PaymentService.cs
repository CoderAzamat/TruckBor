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
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IAppDbContext db,
        ITelegramService tg,
        ILocalizationService loc,
        ILogger<PaymentService> logger)
    {
        _db = db; _tg = tg; _loc = loc; _logger = logger;
    }

    public async Task<bool> ApproveAsync(long paymentId, long adminId, decimal amount, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null) return false;

        payment.Amount = amount;
        payment.Status = PaymentStatus.Approved;
        payment.ApprovedByAdminId = adminId;
        payment.ApprovedAt = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user is not null)
        {
            user.Balance += amount;

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

                var userMsg =
                    $"✅ <b>Balansingiz to'ldirildi!</b>\n\n" +
                    $"💰 Qo'shildi: <b>{amount:N0}</b> so'm\n" +
                    $"💳 Joriy balans: <b>{user.Balance:N0}</b> so'm\n\n" +
                    $"⭐ <b>{payment.Tariff.Name}</b> tarifi faollashtirildi!\n" +
                    $"📅 Muddat: {payment.Tariff.DurationDays} kun";

                try { await _tg.SendMessageAsync(user.TelegramId, userMsg,
                    Keyboards.MainMenu(user.Language), ct); }
                catch (Exception ex)
                { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
            }
            else
            {
                await _db.SaveChangesAsync(ct);

                var userMsg =
                    $"✅ <b>Balansingiz to'ldirildi!</b>\n\n" +
                    $"💰 Qo'shildi: <b>{amount:N0}</b> so'm\n" +
                    $"💳 Joriy balans: <b>{user.Balance:N0}</b> so'm";

                try { await _tg.SendMessageAsync(user.TelegramId, userMsg,
                    Keyboards.MainMenu(user.Language), ct); }
                catch (Exception ex)
                { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
            }
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> RejectAsync(long paymentId, long adminId, string reason, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null) return false;

        payment.Status = PaymentStatus.Rejected;
        payment.RejectionReason = reason;
        payment.ApprovedByAdminId = adminId;
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user is not null)
        {
            try
            {
                await _tg.SendMessageAsync(user.TelegramId,
                    $"❌ <b>To'lovingiz rad etildi.</b>\n\n" +
                    $"Sabab: {reason}\n\n📞 @TruckBorAdmin", ct: ct);
            }
            catch (Exception ex)
            { _logger.LogWarning(ex, "Could not notify user {UserId}", user.TelegramId); }
        }

        return true;
    }
}
