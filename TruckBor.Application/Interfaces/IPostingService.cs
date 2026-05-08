namespace TruckBor.Application.Interfaces;

public interface IPostingService
{
    Task PostToGroupsAsync(long postId, long userId, CancellationToken ct = default);
    Task PostToChannelAsync(long postId, CancellationToken ct = default);
    Task HandleSpamAsync(long telegramAccountId, CancellationToken ct = default);
}