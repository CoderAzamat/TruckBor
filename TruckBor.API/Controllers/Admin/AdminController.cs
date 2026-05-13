using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;
using TruckBor.Domain.Enums;

namespace TruckBor.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : ControllerBase
{
    private readonly IAppDbContext         _db;
    private readonly ITelegramBotClient    _bot;
    private readonly IBalanceService       _balance;
    private readonly IPaymentService       _payment;
    private readonly IPremiumOrderService  _premium;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAppDbContext db, ITelegramBotClient bot,
        IBalanceService balance, IPaymentService payment,
        IPremiumOrderService premium, ILogger<AdminController> logger)
    {
        _db = db; _bot = bot; _balance = balance;
        _payment = payment; _premium = premium; _logger = logger;
    }

    private static DateTime TashkentNow()
    {
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); }
        catch { tz = TimeZoneInfo.Utc; }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    // ═══ DASHBOARD ═══════════════════════════════════════════════════════
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var today     = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalUsers   = await _db.Users.CountAsync(ct);
        var todayUsers   = await _db.Users.CountAsync(u => u.CreatedAt >= today, ct);
        var weekUsers    = await _db.Users.CountAsync(u => u.CreatedAt >= today.AddDays(-7), ct);
        var activeSubs   = await _db.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow, ct);
        var pendingPay   = await _db.Payments.CountAsync(p => p.Status == PaymentStatus.Pending, ct);
        var todayIncome  = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved && p.ApprovedAt >= today).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var monthIncome  = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved && p.ApprovedAt >= thisMonth).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var totalIncome  = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var totalPosts   = await _db.Posts.CountAsync(ct);
        var todayPosts   = await _db.Posts.CountAsync(p => p.CreatedAt >= today, ct);
        var totalGroups  = await _db.Groups.CountAsync(ct);
        var activeGroups = await _db.Groups.CountAsync(g => g.IsActive, ct);

        // Chart: last 7 days revenue
        var chartData = new List<object>();
        for (int i = 6; i >= 0; i--)
        {
            var d     = today.AddDays(-i);
            var dEnd  = d.AddDays(1);
            var amt   = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved && p.ApprovedAt >= d && p.ApprovedAt < dEnd).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
            var users = await _db.Users.CountAsync(u => u.CreatedAt >= d && u.CreatedAt < dEnd, ct);
            chartData.Add(new { date = d.ToString("MM/dd"), amount = amt, users });
        }

        // Recent payments
        var recentPayments = await _db.Payments.Include(p => p.User).Include(p => p.Tariff)
            .OrderByDescending(p => p.CreatedAt).Take(5)
            .Select(p => new {
                p.Id,
                userName  = p.User != null ? p.User.FullName : "—",
                tariff    = p.Tariff != null ? p.Tariff.Name : "Balans",
                p.Amount, p.Status,
                createdAt = p.CreatedAt
            }).ToListAsync(ct);

        return Ok(new
        {
            totalUsers, todayUsers, weekUsers,
            activeSubs, pendingPay,
            todayIncome, monthIncome, totalIncome,
            totalPosts, todayPosts,
            totalGroups, activeGroups,
            serverTime = TashkentNow().ToString("dd.MM.yyyy HH:mm:ss"),
            chartData, recentPayments
        });
    }

    // ═══ USERS ═══════════════════════════════════════════════════════════
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var q = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.FullName.Contains(search) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)) ||
                u.TelegramId.ToString().Contains(search) ||
                u.Id.ToString() == search);

        var total = await q.CountAsync(ct);
        var users = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * limit).Take(limit)
            .Select(u => new {
                u.Id, u.TelegramId, u.FullName, u.PhoneNumber,
                u.Balance, u.IsBlocked, u.IsPremium, u.TotalPosts,
                createdAt   = u.CreatedAt,
                lastActivity = u.LastActivity,
                hasActiveSub = u.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow)
            }).ToListAsync(ct);

        return Ok(new { total, page, limit, users });
    }

    [HttpGet("users/{id:long}")]
    public async Task<IActionResult> GetUser(long id, CancellationToken ct)
    {
        var u = await _db.Users
            .Include(u => u.Subscriptions).ThenInclude(s => s.Tariff)
            .Include(u => u.Payments).ThenInclude(p => p.Tariff)
            .Include(u => u.TelegramAccounts)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (u is null) return NotFound();

        var balanceHistory = await _db.BalanceTransactions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new { x.Amount, x.ReasonCode, x.Description, x.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            u.Id, u.TelegramId, u.FullName, u.PhoneNumber,
            u.Balance, u.IsBlocked, u.IsPremium, u.TotalPosts,
            createdAt    = u.CreatedAt,
            lastActivity = u.LastActivity,
            subscriptions = u.Subscriptions.Select(s => new {
                s.Id, tariff = s.Tariff?.Name, s.Status,
                startDate = s.StartDate, endDate = s.EndDate, s.DaysLeft
            }),
            payments = u.Payments.OrderByDescending(p => p.CreatedAt).Take(20).Select(p => new {
                p.Id, tariff = p.Tariff?.Name ?? "Balans", p.Amount, p.Status, p.Type, createdAt = p.CreatedAt
            }),
            accounts = u.TelegramAccounts.Select(a => new { a.Id, a.PhoneNumber, a.IsActive, a.IsPremium, a.IsSpammed }),
            balanceHistory
        });
    }

    [HttpPost("users/{id:long}/balance")]
    public async Task<IActionResult> UpdateBalance(long id, [FromBody] BalanceRequest req, CancellationToken ct)
    {
        var adminId = GetAdminTgId();
        bool ok;
        if (req.Amount >= 0)
            ok = await _balance.CreditAsync(id, req.Amount, "admin_gift", req.Note ?? "Admin gift", adminId: adminId, ct: ct);
        else
            ok = await _balance.DebitAsync(id, -req.Amount, "admin_debit", req.Note ?? "Admin debit", adminId: adminId, ct: ct);

        if (!ok) return BadRequest(new { error = "Tranzaksiya amalga oshmadi" });

        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        try { await _bot.SendMessage(u!.TelegramId, $"💰 <b>Balans yangilandi</b>\n\n{(req.Amount >= 0 ? "+" : "")}{req.Amount:N0} so'm\n💳 Balans: <b>{u.Balance:N0}</b> so'm\nSabab: {req.Note ?? "Admin"}", parseMode: ParseMode.Html, cancellationToken: ct); } catch { }

        return Ok(new { balance = u?.Balance });
    }

    [HttpPost("users/{id:long}/ban")]
    public async Task<IActionResult> BanUser(long id, [FromBody] BanRequest req, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.IsBlocked = req.Ban;
        await _db.SaveChangesAsync(ct);
        try { await _bot.SendMessage(u.TelegramId, req.Ban ? "🚫 Hisobingiz bloklandi." : "✅ Hisobingiz faollashtirildi.", parseMode: ParseMode.Html, cancellationToken: ct); } catch { }
        return Ok(new { u.IsBlocked });
    }

    [HttpPost("users/{id:long}/message")]
    public async Task<IActionResult> SendUserMessage(long id, [FromBody] MessageRequest req, CancellationToken ct)
    {
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        try { await _bot.SendMessage(u.TelegramId, $"📢 <b>Admin xabari:</b>\n\n{req.Text}", parseMode: ParseMode.Html, cancellationToken: ct); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        return Ok();
    }

    [HttpPost("users/{id:long}/give-tariff")]
    public async Task<IActionResult> GiveTariff(long id, [FromBody] GiveTariffRequest req, CancellationToken ct)
    {
        var tariff = await _db.Tariffs.FindAsync(new object[] { req.TariffId }, ct);
        if (tariff is null) return NotFound(new { error = "Tarif topilmadi" });

        var existing = await _db.Subscriptions.FirstOrDefaultAsync(s =>
            s.UserId == id && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow, ct);

        if (existing is not null)
            existing.EndDate = existing.EndDate.AddDays(tariff.DurationDays);
        else
            _db.Subscriptions.Add(new Subscription { UserId = id, TariffId = tariff.Id, Status = SubscriptionStatus.Active, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(tariff.DurationDays) });

        await _db.SaveChangesAsync(ct);

        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        try { await _bot.SendMessage(u!.TelegramId, $"🎁 <b>Admin sizga tarif berdi!</b>\n\n⭐ {tariff.Name}\n📅 {tariff.DurationDays} kun", parseMode: ParseMode.Html, cancellationToken: ct); } catch { }

        return Ok();
    }

    // ═══ PAYMENTS ════════════════════════════════════════════════════════
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] PaymentStatus? status, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var q = _db.Payments.Include(p => p.User).Include(p => p.Tariff).AsQueryable();
        if (status.HasValue) q = q.Where(p => p.Status == status.Value);

        var total = await q.CountAsync(ct);
        var payments = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * limit).Take(limit)
            .Select(p => new {
                p.Id,
                userName    = p.User != null ? p.User.FullName : "—",
                userPhone   = p.User != null ? p.User.PhoneNumber : null,
                userId      = p.UserId,
                tariff      = p.Tariff != null ? p.Tariff.Name : "Balans to'ldirish",
                p.Amount, p.Status, p.Type, p.ProviderCode,
                p.CheckFileId, p.TransactionId, p.Comment,
                createdAt   = p.CreatedAt
            }).ToListAsync(ct);

        return Ok(new { total, page, limit, payments });
    }

    [HttpPost("payments/{id:long}/approve")]
    public async Task<IActionResult> ApprovePayment(long id, [FromBody] ApproveRequest req, CancellationToken ct)
    {
        var adminId = GetAdminTgId() ?? 0;
        var ok = await _payment.ApproveAsync(id, adminId, req.Amount, ct);
        if (!ok) return BadRequest(new { error = "To'lov topilmadi yoki allaqachon ko'rib chiqilgan" });
        return Ok(new { message = "Tasdiqlandi" });
    }

    [HttpPost("payments/{id:long}/reject")]
    public async Task<IActionResult> RejectPayment(long id, [FromBody] RejectRequest req, CancellationToken ct)
    {
        var adminId = GetAdminTgId() ?? 0;
        var ok = await _payment.RejectAsync(id, adminId, req.Reason ?? "Chek tasdiqlanmadi", ct);
        if (!ok) return BadRequest(new { error = "To'lov topilmadi" });
        return Ok();
    }

    // ═══ TARIFFS ═════════════════════════════════════════════════════════
    [HttpGet("tariffs")]
    public async Task<IActionResult> GetTariffs(CancellationToken ct) =>
        Ok(await _db.Tariffs.OrderBy(t => t.SortOrder).ToListAsync(ct));

    [HttpPost("tariffs")]
    public async Task<IActionResult> CreateTariff([FromBody] TariffRequest req, CancellationToken ct)
    {
        var t = new Tariff { Name = req.Name, Description = req.Description, Price = req.Price, DurationDays = req.DurationDays, MaxAccounts = req.MaxAccounts, MaxGroups = req.MaxGroups, PostsPerDay = req.PostsPerDay, PostIntervalMinutes = req.PostIntervalMinutes, IsRecommended = req.IsRecommended, IsActive = true, SortOrder = req.SortOrder };
        _db.Tariffs.Add(t);
        await _db.SaveChangesAsync(ct);
        return Ok(t);
    }

    [HttpPut("tariffs/{id:long}")]
    public async Task<IActionResult> UpdateTariff(long id, [FromBody] TariffRequest req, CancellationToken ct)
    {
        var t = await _db.Tariffs.FindAsync(new object[] { id }, ct);
        if (t is null) return NotFound();
        t.Name = req.Name; t.Description = req.Description; t.Price = req.Price;
        t.DurationDays = req.DurationDays; t.MaxAccounts = req.MaxAccounts;
        t.MaxGroups = req.MaxGroups; t.PostsPerDay = req.PostsPerDay;
        t.PostIntervalMinutes = req.PostIntervalMinutes;
        t.IsRecommended = req.IsRecommended; t.IsActive = req.IsActive;
        t.SortOrder = req.SortOrder;
        await _db.SaveChangesAsync(ct);
        return Ok(t);
    }

    [HttpDelete("tariffs/{id:long}")]
    public async Task<IActionResult> DeleteTariff(long id, CancellationToken ct)
    {
        var t = await _db.Tariffs.FindAsync(new object[] { id }, ct);
        if (t is null) return NotFound();
        t.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ PAYMENT PROVIDERS ═══════════════════════════════════════════════
    [HttpGet("payment-providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct) =>
        Ok(await _db.PaymentProviders.OrderBy(p => p.SortOrder).ToListAsync(ct));

    [HttpPut("payment-providers/{id:long}")]
    public async Task<IActionResult> UpdateProvider(long id, [FromBody] ProviderUpdateRequest req, CancellationToken ct)
    {
        var p = await _db.PaymentProviders.FindAsync(new object[] { id }, ct);
        if (p is null) return NotFound();
        p.IsActive = req.IsActive;
        if (req.MerchantId    is not null) p.MerchantId    = req.MerchantId;
        if (req.SecretKey     is not null) p.SecretKey     = req.SecretKey;
        if (req.ApiKey        is not null) p.ApiKey        = req.ApiKey;
        if (req.WebhookSecret is not null) p.WebhookSecret = req.WebhookSecret;
        await _db.SaveChangesAsync(ct);
        return Ok(p);
    }

    // ═══ GROUPS ══════════════════════════════════════════════════════════
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups([FromQuery] int page = 1, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var total  = await _db.Groups.CountAsync(ct);
        var groups = await _db.Groups.OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * limit).Take(limit)
            .ToListAsync(ct);
        return Ok(new { total, groups });
    }

    [HttpPost("groups")]
    public async Task<IActionResult> AddGroup([FromBody] GroupRequest req, CancellationToken ct)
    {
        var g = new Group { TelegramGroupId = req.TelegramGroupId, Title = req.Title, IsActive = true };
        _db.Groups.Add(g);
        await _db.SaveChangesAsync(ct);
        return Ok(g);
    }

    [HttpPut("groups/{id:long}/toggle")]
    public async Task<IActionResult> ToggleGroup(long id, CancellationToken ct)
    {
        var g = await _db.Groups.FindAsync(new object[] { id }, ct);
        if (g is null) return NotFound();
        g.IsActive = !g.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new { g.IsActive });
    }

    [HttpDelete("groups/{id:long}")]
    public async Task<IActionResult> DeleteGroup(long id, CancellationToken ct)
    {
        var g = await _db.Groups.FindAsync(new object[] { id }, ct);
        if (g is null) return NotFound();
        _db.Groups.Remove(g);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ CARDS ═══════════════════════════════════════════════════════════
    [HttpGet("cards")]
    public async Task<IActionResult> GetCards(CancellationToken ct) =>
        Ok(await _db.Cards.OrderByDescending(c => c.CreatedAt).ToListAsync(ct));

    [HttpPost("cards")]
    public async Task<IActionResult> AddCard([FromBody] CardRequest req, CancellationToken ct)
    {
        var c = new Card { CardNumber = req.CardNumber, CardHolder = req.CardHolder, BankName = req.BankName, IsActive = true };
        _db.Cards.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(c);
    }

    [HttpPut("cards/{id:long}/toggle")]
    public async Task<IActionResult> ToggleCard(long id, CancellationToken ct)
    {
        var c = await _db.Cards.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        c.IsActive = !c.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new { c.IsActive });
    }

    [HttpDelete("cards/{id:long}")]
    public async Task<IActionResult> DeleteCard(long id, CancellationToken ct)
    {
        var c = await _db.Cards.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        _db.Cards.Remove(c);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ CHANNELS ════════════════════════════════════════════════════════
    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels(CancellationToken ct) =>
        Ok(await _db.Channels.OrderByDescending(c => c.CreatedAt).ToListAsync(ct));

    [HttpPost("channels")]
    public async Task<IActionResult> AddChannel([FromBody] ChannelRequest req, CancellationToken ct)
    {
        var c = new Channel { TelegramChannelId = req.TelegramChannelId, Title = req.Title, IsActive = true, IsRequired = req.IsMandatory };
        _db.Channels.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(c);
    }

    [HttpDelete("channels/{id:long}")]
    public async Task<IActionResult> DeleteChannel(long id, CancellationToken ct)
    {
        var c = await _db.Channels.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        _db.Channels.Remove(c);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ SETTINGS ════════════════════════════════════════════════════════
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _db.Settings.OrderBy(s => s.Key).ToListAsync(ct);
        return Ok(new { settings, serverTime = TashkentNow().ToString("dd.MM.yyyy HH:mm:ss") });
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] SettingRequest req, CancellationToken ct)
    {
        var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (s is null)
        {
            _db.Settings.Add(new Setting { Key = key, Value = req.Value });
        }
        else
        {
            s.Value = req.Value;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { key, value = req.Value });
    }

    // ═══ BROADCAST ═══════════════════════════════════════════════════════
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        var ids = await _db.Users
            .Where(u => !u.IsBlocked && u.IsOnboarded)
            .Select(u => u.TelegramId)
            .ToListAsync(ct);

        int sent = 0, failed = 0;
        foreach (var id in ids)
        {
            try
            {
                await _bot.SendMessage(id, $"📢 <b>TruckBor</b>\n\n{req.Text}",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                sent++;
                await Task.Delay(50, ct); // Rate limiting
            }
            catch { failed++; }
        }
        return Ok(new { total = ids.Count, sent, failed });
    }

    // ═══ ADMIN USERS ═════════════════════════════════════════════════════
    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins(CancellationToken ct) =>
        Ok(await _db.AdminUsers.OrderByDescending(a => a.CreatedAt).Select(a => new {
            a.Id, a.TelegramId, a.FullName, a.Username, a.IsSuper,
            a.CanManageUsers, a.CanManagePayments, a.CanManageTariffs,
            a.CanManageGroups, a.CanManageCards, a.CanManageChannels,
            a.CanBroadcast, a.CanViewStatistics, a.CanManageAdmins,
            a.CanManageSettings, a.CanManageVirtual, a.CanManagePremium,
            a.LastLoginAt, createdAt = a.CreatedAt
        }).ToListAsync(ct));

    [HttpPost("admins")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest req, CancellationToken ct)
    {
        var existing = await _db.AdminUsers.FirstOrDefaultAsync(a => a.TelegramId == req.TelegramId, ct);
        if (existing is not null) return BadRequest(new { error = "Bu admin allaqachon mavjud" });

        var admin = new AdminUser
        {
            TelegramId = req.TelegramId, FullName = req.FullName,
            Username = req.Username,
            PasswordHash = req.Password is not null ? BCrypt.Net.BCrypt.HashPassword(req.Password) : null,
            IsSuper = req.IsSuper,
            CanManageUsers = req.CanManageUsers, CanManagePayments = req.CanManagePayments,
            CanManageTariffs = req.CanManageTariffs, CanManageGroups = req.CanManageGroups,
            CanManageCards = req.CanManageCards, CanManageChannels = req.CanManageChannels,
            CanBroadcast = req.CanBroadcast, CanViewStatistics = true,
            CanManageAdmins = req.CanManageAdmins, CanManageSettings = req.CanManageSettings,
            CanManageVirtual = req.CanManageVirtual, CanManagePremium = req.CanManagePremium,
        };
        _db.AdminUsers.Add(admin);
        await _db.SaveChangesAsync(ct);
        return Ok(new { admin.Id });
    }

    [HttpDelete("admins/{id:long}")]
    public async Task<IActionResult> DeleteAdmin(long id, CancellationToken ct)
    {
        var a = await _db.AdminUsers.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        _db.AdminUsers.Remove(a);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ VIDEOS ══════════════════════════════════════════════════════════
    [HttpGet("videos")]
    public async Task<IActionResult> GetVideos(CancellationToken ct) =>
        Ok(await _db.VideoTutorials.OrderBy(v => v.ServiceKey).ThenBy(v => v.SortOrder).ToListAsync(ct));

    [HttpPost("videos")]
    public async Task<IActionResult> AddVideo([FromBody] VideoRequest req, CancellationToken ct)
    {
        var v = new VideoTutorial { Title = req.Title, TitleRu = req.TitleRu, ServiceKey = req.ServiceKey, VideoUrl = req.VideoUrl, Description = req.Description, IsActive = true, SortOrder = req.SortOrder };
        _db.VideoTutorials.Add(v);
        await _db.SaveChangesAsync(ct);
        return Ok(v);
    }

    [HttpPut("videos/{id:long}")]
    public async Task<IActionResult> UpdateVideo(long id, [FromBody] VideoRequest req, CancellationToken ct)
    {
        var v = await _db.VideoTutorials.FindAsync(new object[] { id }, ct);
        if (v is null) return NotFound();
        v.Title = req.Title; v.TitleRu = req.TitleRu; v.ServiceKey = req.ServiceKey;
        v.VideoUrl = req.VideoUrl; v.Description = req.Description;
        v.IsActive = req.IsActive; v.SortOrder = req.SortOrder;
        await _db.SaveChangesAsync(ct);
        return Ok(v);
    }

    [HttpDelete("videos/{id:long}")]
    public async Task<IActionResult> DeleteVideo(long id, CancellationToken ct)
    {
        var v = await _db.VideoTutorials.FindAsync(new object[] { id }, ct);
        if (v is null) return NotFound();
        _db.VideoTutorials.Remove(v);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ═══ PREMIUM ORDERS ══════════════════════════════════════════════════
    [HttpGet("premium")]
    public async Task<IActionResult> GetPremiumOrders([FromQuery] string? status, CancellationToken ct = default)
    {
        var q = _db.PremiumOrders.Include(x => x.User).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        var orders = await q.OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new { x.Id, userName = x.User != null ? x.User.FullName : "—", x.TargetUsername, x.MonthCount, x.AmountPaid, x.Status, x.PaymentMethod, createdAt = x.CreatedAt })
            .ToListAsync(ct);
        return Ok(orders);
    }

    [HttpPost("premium/{id:long}/complete")]
    public async Task<IActionResult> CompletePremium(long id, [FromBody] PremiumActionRequest req, CancellationToken ct)
    {
        var adminId = GetAdminTgId() ?? 0;
        var ok = await _premium.CompleteOrderAsync(id, adminId, req.Note, ct);
        if (!ok) return BadRequest(new { error = "Buyurtma topilmadi yoki holati noto'g'ri" });
        return Ok();
    }

    [HttpPost("premium/{id:long}/reject")]
    public async Task<IActionResult> RejectPremium(long id, [FromBody] RejectRequest req, CancellationToken ct)
    {
        var adminId = GetAdminTgId() ?? 0;
        var ok = await _premium.RejectOrderAsync(id, adminId, req.Reason ?? "Rad etildi", ct);
        if (!ok) return BadRequest(new { error = "Buyurtma topilmadi" });
        return Ok();
    }

    // ═══ VIRTUAL NUMBERS ═════════════════════════════════════════════════
    [HttpGet("virtual-numbers")]
    public async Task<IActionResult> GetVirtualOrders(CancellationToken ct)
    {
        var orders = await _db.VirtualNumberOrders.Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, userName = x.User != null ? x.User.FullName : "—", x.PhoneNumber, x.CountryCode, x.Service, x.Status, x.AmountPaid, x.SmsCode, createdAt = x.CreatedAt })
            .ToListAsync(ct);
        return Ok(orders);
    }

    // ═══ EXPORT PDF ═══════════════════════════════════════════════════════
    [HttpGet("export/users")]
    public async Task<IActionResult> ExportUsers(CancellationToken ct)
    {
        var html = await GenerateUsersPdfAsync(ct);
        return File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8",
            $"users_{TashkentNow():yyyyMMdd_HHmmss}.html");
    }

    [HttpGet("export/payments")]
    public async Task<IActionResult> ExportPayments(CancellationToken ct)
    {
        var html = await GeneratePaymentsPdfAsync(ct);
        return File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8",
            $"payments_{TashkentNow():yyyyMMdd_HHmmss}.html");
    }

    // ═══ LOGO UPLOAD ══════════════════════════════════════════════════════
    [HttpPost("settings/logo")]
    public async Task<IActionResult> UploadLogo([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Fayl tanlanmadi" });
        if (file.Length > 2 * 1024 * 1024) return BadRequest(new { error = "Fayl 2MB dan katta bo'lmasligi kerak" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".svg", ".webp" }.Contains(ext))
            return BadRequest(new { error = "Fayl turi qo'llab-quvvatlanmaydi" });

        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logo");
        Directory.CreateDirectory(uploads);
        var fileName = $"logo{ext}";
        var path = Path.Combine(uploads, fileName);

        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs, ct);

        var url = $"/uploads/logo/{fileName}";
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "logo_url", ct);
        if (setting is null) _db.Settings.Add(new Setting { Key = "logo_url", Value = url });
        else setting.Value = url;
        await _db.SaveChangesAsync(ct);

        return Ok(new { url });
    }

    // ═══ HELPERS ══════════════════════════════════════════════════════════
    private long? GetAdminTgId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }

    private async Task<string> GenerateUsersPdfAsync(CancellationToken ct)
    {
        var users = await _db.Users.Include(u => u.Subscriptions).ThenInclude(s => s.Tariff)
            .OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        var now     = TashkentNow();
        var totalRev = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html><html lang='uz'><head><meta charset='UTF-8'/>
<style>*{{margin:0;padding:0;box-sizing:border-box}}body{{font-family:'Segoe UI',sans-serif;background:#fff;color:#1a1a2e;font-size:11px}}
.header{{background:linear-gradient(135deg,#378ADD,#185FA5);color:#fff;padding:24px 32px}}
.header h1{{font-size:22px;font-weight:700;margin-bottom:4px}}.header p{{font-size:12px;opacity:.85}}
.summary{{display:flex;border-bottom:2px solid #e2e8f0}}
.sc{{flex:1;padding:16px 20px;text-align:center;border-right:1px solid #e2e8f0}}
.sc:last-child{{border-right:none}}.sc .v{{font-size:22px;font-weight:700;color:#378ADD}}.sc .l{{font-size:11px;color:#64748b;margin-top:2px}}
.tw{{padding:20px 24px}}table{{width:100%;border-collapse:collapse}}
thead tr{{background:#378ADD;color:#fff}}thead th{{padding:9px 10px;text-align:left;font-size:10.5px;font-weight:600}}
tbody tr{{border-bottom:1px solid #f1f5f9}}tbody tr:nth-child(even){{background:#f8fafc}}
tbody td{{padding:8px 10px}}.badge{{display:inline-block;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:600}}
.bv{{background:#d1fae5;color:#065f46}}.bn{{background:#f1f5f9;color:#64748b}}.bb{{background:#fee2e2;color:#991b1b}}
.footer{{background:#f8fafc;border-top:1px solid #e2e8f0;padding:12px 24px;font-size:10px;color:#94a3b8;display:flex;justify-content:space-between}}
@media print{{body{{-webkit-print-color-adjust:exact;print-color-adjust:exact}}}}
</style></head><body>
<div class='header'><h1>📊 TruckBor — Foydalanuvchilar Ro'yxati</h1><p>Hisobot vaqti: {now:dd.MM.yyyy HH:mm:ss} (Toshkent vaqti)</p></div>
<div class='summary'>
  <div class='sc'><div class='v'>{users.Count:N0}</div><div class='l'>Jami</div></div>
  <div class='sc'><div class='v'>{users.Count(u => u.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow)):N0}</div><div class='l'>Faol VIP</div></div>
  <div class='sc'><div class='v'>{totalRev:N0}</div><div class='l'>Daromad (UZS)</div></div>
  <div class='sc'><div class='v'>{now:dd.MM.yyyy}</div><div class='l'>Sana</div></div>
</div>
<div class='tw'><table><thead><tr><th>#</th><th>ID</th><th>Ism</th><th>Telefon</th><th>Balans</th><th>VIP</th><th>Tugash</th><th>Ro'yxat</th><th>Holat</th></tr></thead><tbody>");

        int row = 1;
        foreach (var u in users)
        {
            var sub = u.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow).OrderByDescending(s => s.EndDate).FirstOrDefault();
            var badge = u.IsBlocked ? "<span class='badge bb'>Bloklangan</span>" : sub is not null ? $"<span class='badge bv'>✓ {sub.Tariff?.Name ?? "VIP"}</span>" : "<span class='badge bn'>Yo'q</span>";
            sb.Append($"<tr><td>{row++}</td><td><b>{u.Id}</b></td><td>{Enc(u.FullName)}</td><td>{Enc(u.PhoneNumber)}</td><td>{u.Balance:N0}</td><td>{badge}</td><td>{(sub is not null ? sub.EndDate.ToString("dd.MM.yyyy") : "—")}</td><td>{u.CreatedAt:dd.MM.yyyy HH:mm}</td><td>{(u.IsBlocked ? "🚫" : "✅")}</td></tr>");
        }

        sb.Append($"</tbody></table></div><div class='footer'><span>TruckBor Admin Panel</span><span>Jami: {users.Count} ta foydalanuvchi</span><span>{now:dd.MM.yyyy HH:mm}</span></div></body></html>");
        return sb.ToString();
    }

    private async Task<string> GeneratePaymentsPdfAsync(CancellationToken ct)
    {
        var payments = await _db.Payments.Include(p => p.User).Include(p => p.Tariff).OrderByDescending(p => p.CreatedAt).Take(500).ToListAsync(ct);
        var now = TashkentNow();
        var approved = payments.Where(p => p.Status == PaymentStatus.Approved).Sum(p => p.Amount);

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html><html lang='uz'><head><meta charset='UTF-8'/>
<style>*{{margin:0;padding:0;box-sizing:border-box}}body{{font-family:'Segoe UI',sans-serif;background:#fff;color:#1a1a2e;font-size:11px}}
.header{{background:linear-gradient(135deg,#059669,#10b981);color:#fff;padding:24px 32px}}
.header h1{{font-size:22px;font-weight:700;margin-bottom:4px}}.header p{{font-size:12px;opacity:.85}}
.summary{{display:flex;border-bottom:2px solid #e2e8f0}}
.sc{{flex:1;padding:16px 20px;text-align:center;border-right:1px solid #e2e8f0}}
.sc .v{{font-size:20px;font-weight:700;color:#059669}}.sc .l{{font-size:11px;color:#64748b;margin-top:2px}}
.tw{{padding:20px 24px}}table{{width:100%;border-collapse:collapse}}
thead tr{{background:#059669;color:#fff}}thead th{{padding:9px 10px;text-align:left;font-size:10.5px;font-weight:600}}
tbody tr{{border-bottom:1px solid #f1f5f9}}tbody tr:nth-child(even){{background:#f0fdf4}}
tbody td{{padding:7px 10px}}
.s-ok{{color:#065f46;background:#d1fae5;padding:2px 7px;border-radius:12px;font-size:10px;font-weight:600}}
.s-pnd{{color:#92400e;background:#fef3c7;padding:2px 7px;border-radius:12px;font-size:10px;font-weight:600}}
.s-rej{{color:#991b1b;background:#fee2e2;padding:2px 7px;border-radius:12px;font-size:10px;font-weight:600}}
.footer{{background:#f8fafc;border-top:1px solid #e2e8f0;padding:12px 24px;font-size:10px;color:#94a3b8;display:flex;justify-content:space-between}}
</style></head><body>
<div class='header'><h1>💳 TruckBor — To'lovlar Tarixi</h1><p>Hisobot vaqti: {now:dd.MM.yyyy HH:mm:ss} (Toshkent vaqti)</p></div>
<div class='summary'>
  <div class='sc'><div class='v'>{payments.Count}</div><div class='l'>Jami</div></div>
  <div class='sc'><div class='v'>{payments.Count(p => p.Status == PaymentStatus.Approved)}</div><div class='l'>Tasdiqlangan</div></div>
  <div class='sc'><div class='v'>{payments.Count(p => p.Status == PaymentStatus.Pending)}</div><div class='l'>Kutmoqda</div></div>
  <div class='sc'><div class='v'>{approved:N0} UZS</div><div class='l'>Daromad</div></div>
</div>
<div class='tw'><table><thead><tr><th>#</th><th>ID</th><th>Foydalanuvchi</th><th>Telefon</th><th>Tarif</th><th>Summa</th><th>Tur</th><th>Holat</th><th>Vaqt</th></tr></thead><tbody>");

        int row = 1;
        foreach (var p in payments)
        {
            var st = p.Status switch { PaymentStatus.Approved => "<span class='s-ok'>Tasdiqlangan</span>", PaymentStatus.Pending => "<span class='s-pnd'>Kutmoqda</span>", _ => "<span class='s-rej'>Rad etilgan</span>" };
            sb.Append($"<tr><td>{row++}</td><td>{p.Id}</td><td>{Enc(p.User?.FullName)}</td><td>{Enc(p.User?.PhoneNumber)}</td><td>{Enc(p.Tariff?.Name ?? "Balans")}</td><td><b>{p.Amount:N0}</b></td><td>{p.Type}</td><td>{st}</td><td>{p.CreatedAt:dd.MM.yyyy HH:mm}</td></tr>");
        }

        sb.Append($"</tbody></table></div><div class='footer'><span>TruckBor Admin Panel</span><span>Jami: {payments.Count} | Tasdiqlangan: {approved:N0} UZS</span><span>{now:dd.MM.yyyy HH:mm}</span></div></body></html>");
        return sb.ToString();
    }

    private static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}

// ═══ REQUEST MODELS ══════════════════════════════════════════════════════
public record BalanceRequest(decimal Amount, string? Note);
public record BanRequest(bool Ban);
public record MessageRequest(string Text);
public record ApproveRequest(decimal Amount);
public record RejectRequest(string? Reason);
public record GiveTariffRequest(long TariffId);
public record TariffRequest(string Name, string? Description, decimal Price, int DurationDays, int MaxAccounts, int MaxGroups, int PostsPerDay, int PostIntervalMinutes = 30, bool IsRecommended = false, bool IsActive = true, int SortOrder = 0);
public record ProviderUpdateRequest(bool IsActive, string? MerchantId, string? SecretKey, string? ApiKey, string? WebhookSecret);
public record GroupRequest(long TelegramGroupId, string Title);
public record CardRequest(string CardNumber, string CardHolder, string? BankName);
public record ChannelRequest(long TelegramChannelId, string Title, bool IsMandatory = false);
public record SettingRequest(string Value);
public record BroadcastRequest(string Text);
public record CreateAdminRequest(long TelegramId, string FullName, string? Username, string? Password, bool IsSuper = false, bool CanManageUsers = true, bool CanManagePayments = true, bool CanManageTariffs = false, bool CanManageGroups = false, bool CanManageCards = false, bool CanManageChannels = false, bool CanBroadcast = false, bool CanManageAdmins = false, bool CanManageSettings = false, bool CanManageVirtual = false, bool CanManagePremium = false);
public record VideoRequest(string Title, string? TitleRu, string ServiceKey, string VideoUrl, string? Description, bool IsActive = true, int SortOrder = 0);
public record PremiumActionRequest(string? Note);
