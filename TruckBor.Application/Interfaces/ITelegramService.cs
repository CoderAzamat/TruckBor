using Telegram.Bot.Types.ReplyMarkups;

namespace TruckBor.Application.Interfaces;

public interface ITelegramService
{
    Task SendMessageAsync(long chatId, string text, ReplyMarkup? markup = null, CancellationToken ct = default);
    Task SendPhotoAsync(long chatId, string fileId, string? caption = null, ReplyMarkup? markup = null, CancellationToken ct = default);
    Task SendDocumentAsync(long chatId, string fileId, string? caption = null, ReplyMarkup? markup = null, CancellationToken ct = default);
    Task EditMessageAsync(long chatId, int messageId, string text, ReplyMarkup? markup = null, CancellationToken ct = default);
    Task DeleteMessageAsync(long chatId, int messageId, CancellationToken ct = default);
    Task AnswerCallbackAsync(string callbackId, string? text = null, CancellationToken ct = default);
    Task SendMessageToChannelAsync(long channelId, string text, CancellationToken ct = default);
}