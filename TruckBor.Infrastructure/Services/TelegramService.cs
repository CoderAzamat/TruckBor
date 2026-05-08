using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TruckBor.Application.Interfaces;

namespace TruckBor.Infrastructure.Services;

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _bot;

    public TelegramService(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task SendMessageAsync(long chatId, string text, ReplyMarkup? markup = null, CancellationToken ct = default)
    {
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
    }

    public async Task SendPhotoAsync(long chatId, string fileId, string? caption = null, ReplyMarkup? markup = null, CancellationToken ct = default)
    {
        await _bot.SendPhoto(chatId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
    }

    public async Task SendDocumentAsync(long chatId, string fileId, string? caption = null, ReplyMarkup? markup = null, CancellationToken ct = default)
    {
        await _bot.SendDocument(chatId, fileId, caption: caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
    }

    public async Task EditMessageAsync(long chatId, int messageId, string text, ReplyMarkup? markup = null, CancellationToken ct = default)
    {
        await _bot.EditMessageText(chatId, messageId, text, parseMode: ParseMode.Html,
            replyMarkup: markup as InlineKeyboardMarkup, cancellationToken: ct);
    }

    public async Task DeleteMessageAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        await _bot.DeleteMessage(chatId, messageId, cancellationToken: ct);
    }

    public async Task AnswerCallbackAsync(string callbackId, string? text = null, CancellationToken ct = default)
    {
        await _bot.AnswerCallbackQuery(callbackId, text, cancellationToken: ct);
    }

    public async Task SendMessageToChannelAsync(long channelId, string text, CancellationToken ct = default)
    {
        await _bot.SendMessage(channelId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }
}