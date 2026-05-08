using TruckBor.Domain.Enums;

namespace TruckBor.Application.Interfaces;

public interface IUserStateService
{
    Task<UserState> GetStateAsync(long telegramId, CancellationToken ct = default);
    Task SetStateAsync(long telegramId, UserState state, object? data = null, CancellationToken ct = default);
    Task<T?> GetStateDataAsync<T>(long telegramId, CancellationToken ct = default);
    Task ClearStateAsync(long telegramId, CancellationToken ct = default);
    Task<bool> IsFloodingAsync(long telegramId, CancellationToken ct = default);
}