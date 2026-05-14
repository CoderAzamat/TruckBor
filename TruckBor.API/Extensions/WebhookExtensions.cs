using Telegram.Bot;

namespace TruckBor.API.Extensions;

public static class WebhookExtensions
{
    public static async Task SetupWebhookAsync(
        this IServiceProvider services,
        IConfiguration config,
        ILogger logger)
    {
        var webhookUrl = config["Bot:WebhookUrl"];

        // If no webhook URL configured → skip (pure polling mode for local dev)
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogInformation("Bot:WebhookUrl is empty — webhook skipped (local dev mode)");
            return;
        }

        try
        {
            var bot = services.GetRequiredService<ITelegramBotClient>();

            // Delete any old webhook first
            await bot.DeleteWebhook(dropPendingUpdates: true);

            // Set new webhook
            await bot.SetWebhook(webhookUrl,
                allowedUpdates: [],
                dropPendingUpdates: true);

            var info = await bot.GetWebhookInfo();
            logger.LogInformation("✅ Webhook set: {Url} | Pending: {Pending}",
                webhookUrl, info.PendingUpdateCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Webhook setup failed — bot will NOT receive updates");
        }
    }
}
