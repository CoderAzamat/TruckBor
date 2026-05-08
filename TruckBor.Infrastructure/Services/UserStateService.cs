using System.Text.Json;
using StackExchange.Redis;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;

namespace TruckBor.Infrastructure.Services;

public class UserStateService : IUserStateService
{
    private readonly IDatabase _db;
    private const string StatePrefix = "state:";
    private const string DataPrefix = "state_data:";
    private const string FloodPrefix = "flood:";

    public UserStateService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<UserState> GetStateAsync(long telegramId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync($"{StatePrefix}{telegramId}");
        if (value.IsNullOrEmpty) return UserState.None;
        return Enum.TryParse<UserState>(value!, out var state) ? state : UserState.None;
    }

    public async Task SetStateAsync(long telegramId, UserState state, object? data = null, CancellationToken ct = default)
    {
        await _db.StringSetAsync($"{StatePrefix}{telegramId}", state.ToString(), TimeSpan.FromHours(24));
        if (data != null)
        {
            var json = JsonSerializer.Serialize(data);
            await _db.StringSetAsync($"{DataPrefix}{telegramId}", json, TimeSpan.FromHours(24));
        }
    }

    public async Task<T?> GetStateDataAsync<T>(long telegramId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync($"{DataPrefix}{telegramId}");
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!.ToString());
    }

    public async Task ClearStateAsync(long telegramId, CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync($"{StatePrefix}{telegramId}");
        await _db.KeyDeleteAsync($"{DataPrefix}{telegramId}");
    }

    public async Task<bool> IsFloodingAsync(long telegramId, CancellationToken ct = default)
    {
        var key = $"{FloodPrefix}{telegramId}";
        var count = await _db.StringIncrementAsync(key);
        if (count == 1) await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(3));
        return count > 5;
    }
}