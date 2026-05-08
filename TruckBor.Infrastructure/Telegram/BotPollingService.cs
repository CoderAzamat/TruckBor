using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TruckBor.Infrastructure.Telegram.Handlers;

namespace TruckBor.Infrastructure.Telegram;

public class BotPollingService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BotPollingService> _logger;

    public BotPollingService(
        ITelegramBotClient bot,
        IServiceScopeFactory scopeFactory,
        ILogger<BotPollingService> logger)
    {
        _bot = bot;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var me = await _bot.GetMe(ct);
        _logger.LogInformation("Bot polling started: @{Username}", me.Username);

        var options = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = true,
        };

        _bot.StartReceiving(
            updateHandler: async (_, update, token) =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
                await handler.HandleUpdateAsync(update, token);
            },
            errorHandler: (_, ex, _, _) =>
            {
                _logger.LogError(ex, "Polling error");
                return Task.CompletedTask;
            },
            receiverOptions: options,
            cancellationToken: ct);

        await Task.Delay(Timeout.Infinite, ct);
    }
}
