using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Services;

/// <summary>
/// SMS-Activate API integratsiyasi.
/// API hujjati: https://sms-activate.guru/en/api2
/// </summary>
public class VirtualNumberService : IVirtualNumberService
{
    private readonly IAppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IBalanceService _balance;
    private readonly ILogger<VirtualNumberService> _logger;

    private const string BaseUrl = "https://api.sms-activate.guru/stubs/handler_api.php";

    // Mamlakat kodi → SMS-Activate country ID
    private static readonly Dictionary<string, int> CountryIds = new()
    {
        ["uz"] = 156, ["ru"] = 0,  ["kz"] = 106, ["uk"] = 16,
        ["in"] = 22,  ["pl"] = 15, ["us"] = 187, ["de"] = 43,
        ["fr"] = 78,  ["ua"] = 1,
    };

    // Mamlakat kodi → emoji + nom
    private static readonly Dictionary<string, (string emoji, string name)> CountryInfo = new()
    {
        ["uz"] = ("🇺🇿", "O'zbekiston"),
        ["ru"] = ("🇷🇺", "Rossiya"),
        ["kz"] = ("🇰🇿", "Qozog'iston"),
        ["uk"] = ("🇬🇧", "Britaniya"),
        ["in"] = ("🇮🇳", "Hindiston"),
        ["pl"] = ("🇵🇱", "Polsha"),
        ["us"] = ("🇺🇸", "AQSH"),
        ["de"] = ("🇩🇪", "Germaniya"),
        ["fr"] = ("🇫🇷", "Fransiya"),
        ["ua"] = ("🇺🇦", "Ukraina"),
    };

    public VirtualNumberService(IAppDbContext db, IHttpClientFactory http, IBalanceService balance, ILogger<VirtualNumberService> logger)
    {
        _db = db; _http = http; _balance = balance; _logger = logger;
    }

    private async Task<string?> GetApiKeyAsync(CancellationToken ct)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "smsactivate_api_key", ct);
        return string.IsNullOrWhiteSpace(setting?.Value) ? null : setting.Value;
    }

    private async Task<decimal> GetCountryPriceAsync(string countryCode, CancellationToken ct)
    {
        var key = $"vnumber_{countryCode}_price";
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
        return decimal.TryParse(setting?.Value, out var price) ? price : 2000;
    }

    public async Task<List<VirtualNumberCountryDto>> GetCountriesAsync(CancellationToken ct = default)
    {
        var result = new List<VirtualNumberCountryDto>();
        foreach (var (code, (emoji, name)) in CountryInfo)
        {
            var price = await GetCountryPriceAsync(code, ct);
            result.Add(new VirtualNumberCountryDto(code, name, emoji, price));
        }
        return result.OrderBy(x => x.Price).ToList();
    }

    public async Task<VirtualNumberResult> BuyNumberAsync(long userId, string countryCode, string service, CancellationToken ct = default)
    {
        var apiKey = await GetApiKeyAsync(ct);
        if (string.IsNullOrEmpty(apiKey))
            return new VirtualNumberResult(false, "SMS-Activate API kaliti sozlanmagan. Admin bilan bog'laning.", null, null);

        var price = await GetCountryPriceAsync(countryCode, ct);

        // Balansni yechish
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return new VirtualNumberResult(false, "Foydalanuvchi topilmadi", null, null);
        if (user.Balance < price) return new VirtualNumberResult(false, $"Balansingiz yetarli emas. Kerak: {price:N0} so'm, mavjud: {user.Balance:N0} so'm", null, null);

        if (!CountryIds.TryGetValue(countryCode, out var countryId))
            return new VirtualNumberResult(false, "Mamlakat topilmadi", null, null);

        // SMS-Activate dan raqam olish
        try
        {
            var client = _http.CreateClient("smsactivate");
            var url = $"{BaseUrl}?api_key={apiKey}&action=getNumber&service={service}&country={countryId}";
            var response = await client.GetStringAsync(url, ct);

            // Response: "ACCESS_NUMBER:12345678:79001234567" yoki "NO_NUMBERS" yoki boshqa xato
            if (response.StartsWith("ACCESS_NUMBER:"))
            {
                var parts = response.Split(':');
                if (parts.Length >= 3 && long.TryParse(parts[1], out var activationId))
                {
                    var phone = parts[2];

                    // Balansdan yechish
                    var deducted = await _balance.DebitAsync(userId, price, "virtual_number",
                        $"{CountryInfo.GetValueOrDefault(countryCode).name ?? countryCode} — {service.ToUpper()} raqam",
                        ct: ct);
                    if (!deducted) return new VirtualNumberResult(false, "Balans yechishda xato", null, null);

                    // Tarixni saqlash
                    var order = new VirtualNumberOrder
                    {
                        UserId       = userId,
                        ActivationId = activationId.ToString(),
                        PhoneNumber  = phone,
                        CountryCode  = countryCode,
                        Service      = service,
                        Status       = "pending",
                        AmountPaid   = price,
                        ExpiresAt    = DateTime.UtcNow.AddMinutes(20),
                    };
                    _db.VirtualNumberOrders.Add(order);
                    await _db.SaveChangesAsync(ct);

                    return new VirtualNumberResult(true, null, order.Id, phone);
                }
            }

            var errorMsg = response switch
            {
                "NO_NUMBERS"   => "Hozirda ushbu mamlakat uchun raqamlar mavjud emas.",
                "NO_BALANCE"   => "SMS-Activate hisobida mablag' yetarli emas.",
                "BAD_SERVICE"  => "Xizmat topilmadi.",
                "BAD_KEY"      => "API kalit noto'g'ri.",
                _              => $"Xato: {response}"
            };
            return new VirtualNumberResult(false, errorMsg, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS-Activate API error for user {UserId}", userId);
            return new VirtualNumberResult(false, "API bilan bog'lanishda xato. Qaytadan urinib ko'ring.", null, null);
        }
    }

    public async Task<VirtualNumberStatusDto?> CheckStatusAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _db.VirtualNumberOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return null;

        var apiKey = await GetApiKeyAsync(ct);
        if (string.IsNullOrEmpty(apiKey)) return new VirtualNumberStatusDto(order.Status, order.SmsCode, order.PhoneNumber);

        try
        {
            var client = _http.CreateClient("smsactivate");
            var url = $"{BaseUrl}?api_key={apiKey}&action=getStatus&id={order.ActivationId}";
            var response = await client.GetStringAsync(url, ct);

            string status = order.Status;
            string? smsCode = order.SmsCode;

            if (response.StartsWith("STATUS_OK:"))
            {
                smsCode = response.Split(':')[1];
                status = "received";
                order.SmsCode = smsCode;
                order.Status = status;
                order.SmsReceivedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            else if (response == "STATUS_WAIT_CODE") status = "pending";
            else if (response == "STATUS_CANCEL") status = "cancelled";
            else if (response == "STATUS_WAIT_RETRY:") status = "pending";

            return new VirtualNumberStatusDto(status, smsCode, order.PhoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check error for order {OrderId}", orderId);
            return new VirtualNumberStatusDto(order.Status, order.SmsCode, order.PhoneNumber);
        }
    }

    public async Task<bool> CancelAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _db.VirtualNumberOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null || order.Status == "done") return false;

        var apiKey = await GetApiKeyAsync(ct);
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(order.ActivationId))
        {
            try
            {
                var client = _http.CreateClient("smsactivate");
                await client.GetStringAsync($"{BaseUrl}?api_key={apiKey}&action=setStatus&status=8&id={order.ActivationId}", ct);
            }
            catch { /* silently ignore */ }
        }

        // Balansni qaytarish (agar sms kelmagan bo'lsa)
        if (order.Status == "pending" && order.AmountPaid > 0)
        {
            await _balance.CreditAsync(order.UserId, order.AmountPaid, "refund",
                "Virtual raqam bekor qilindi — pul qaytarildi", ct: ct);
        }

        order.Status = "cancelled";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<VirtualNumberOrderDto>> GetUserOrdersAsync(long userId, CancellationToken ct = default)
    {
        return await _db.VirtualNumberOrders
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new VirtualNumberOrderDto(
                x.Id, x.CountryCode, x.PhoneNumber, x.Status,
                x.AmountPaid, x.CreatedAt, x.SmsCode))
            .ToListAsync(ct);
    }
}
