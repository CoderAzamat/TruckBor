using Telegram.Bot;

namespace TruckBor.API.Extensions;

public static class WebhookExtensions
{
    public static async Task SetupWebhookAsync(
        this IServiceProvider services,
        IConfiguration config,
        ILogger logger)
    {
        if (config.GetValue<bool>("Bot:UsePolling"))
        {
            logger.LogInformation("Polling mode — webhook skipped");
            return;
        }

        try
        {
            var bot = services.GetRequiredService<ITelegramBotClient>();
            var webhookUrl = config["Bot:WebhookUrl"]!;

            await bot.SetWebhook(webhookUrl,
                allowedUpdates: [],
                dropPendingUpdates: true);

            logger.LogInformation("Webhook set: {Url}", webhookUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook setup failed — bot will not receive updates via webhook");
        }
    }
}