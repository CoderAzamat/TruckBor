namespace TruckBor.Application.Interfaces;

public interface IVirtualNumberService
{
    /// <summary>Mamlakat uchun mavjud xizmatlar va narxlarni olish.</summary>
    Task<List<VirtualNumberCountryDto>> GetCountriesAsync(CancellationToken ct = default);

    /// <summary>Virtual raqam buyurtma qilish. Balansdan yechadi.</summary>
    Task<VirtualNumberResult> BuyNumberAsync(long userId, string countryCode, string service, CancellationToken ct = default);

    /// <summary>Aktivatsiya holatini va SMS kodni tekshirish.</summary>
    Task<VirtualNumberStatusDto?> CheckStatusAsync(long orderId, CancellationToken ct = default);

    /// <summary>Aktivatsiyani bekor qilish va balansni qaytarish.</summary>
    Task<bool> CancelAsync(long orderId, CancellationToken ct = default);

    /// <summary>Foydalanuvchining virtual raqam tarixini olish.</summary>
    Task<List<VirtualNumberOrderDto>> GetUserOrdersAsync(long userId, CancellationToken ct = default);
}

public record VirtualNumberCountryDto(string Code, string Name, string Emoji, decimal Price);
public record VirtualNumberResult(bool Success, string? Error, long? OrderId, string? PhoneNumber);
public record VirtualNumberStatusDto(string Status, string? SmsCode, string? PhoneNumber);
public record VirtualNumberOrderDto(long Id, string CountryCode, string? PhoneNumber, string Status, decimal AmountPaid, DateTime CreatedAt, string? SmsCode);
