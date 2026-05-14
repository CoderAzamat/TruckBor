using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;

namespace TruckBor.Infrastructure.Services;

/// <summary>
/// Calls the Pyrogram HTTP microservice for Telegram account operations:
/// SMS auth, group joining, message sending, scraping, anti-spam.
/// </summary>
public class TelegramSmsAuthService : ITelegramSmsAuthService
{
    private readonly HttpClient _http;
    private readonly string?    _baseUrl;
    private readonly ILogger<TelegramSmsAuthService> _logger;

    public TelegramSmsAuthService(
        IHttpClientFactory httpFactory,
        IConfiguration     config,
        ILogger<TelegramSmsAuthService> logger)
    {
        _http    = httpFactory.CreateClient("pyro");
        _http.Timeout = TimeSpan.FromMinutes(5); // batch operations can be slow
        _baseUrl = config["TelegramAuth:ServiceUrl"]?.TrimEnd('/');
        _logger  = logger;
    }

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl);

    // ═══ AUTH ═════════════════════════════════════════════════════════════════

    public async Task<SmsCodeResult> SendCodeAsync(string phone, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsCodeResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/send_code", new { phone }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroSendCodeResponse>(cancellationToken: ct);
            if (body is null) return new SmsCodeResult(false, Error: "Bo'sh javob");

            return body.ok
                ? new SmsCodeResult(true, PhoneCodeHash: body.phone_code_hash ?? "")
                : new SmsCodeResult(false, Error: body.error ?? "Noma'lum xato");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendCode failed for {Phone}", phone);
            return new SmsCodeResult(false, Error: ex.Message);
        }
    }

    public async Task<SmsVerifyResult> VerifyCodeAsync(
        string phone, string code, string phoneCodeHash, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsVerifyResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/verify_code",
                new { phone, code, phone_code_hash = phoneCodeHash }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroVerifyResponse>(cancellationToken: ct);
            if (body is null) return new SmsVerifyResult(false, Error: "Bo'sh javob");

            if (body.ok)
                return new SmsVerifyResult(true,
                    SessionString: body.session_string ?? "",
                    TelegramId: body.user?.id ?? 0,
                    FirstName: body.user?.first_name ?? "",
                    LastName: body.user?.last_name ?? "",
                    Username: body.user?.username ?? "",
                    IsPremium: body.user?.is_premium ?? false);

            if (body.needs_2fa) return new SmsVerifyResult(false, Needs2FA: true);
            return new SmsVerifyResult(false, Error: body.error ?? "Tasdiqlash xatosi");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyCode failed for {Phone}", phone);
            return new SmsVerifyResult(false, Error: ex.Message);
        }
    }

    public async Task<SmsVerifyResult> Verify2FaAsync(
        string phone, string password, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsVerifyResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/verify_2fa",
                new { phone, password }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroVerifyResponse>(cancellationToken: ct);
            if (body is null) return new SmsVerifyResult(false, Error: "Bo'sh javob");

            return body.ok
                ? new SmsVerifyResult(true,
                    SessionString: body.session_string ?? "",
                    TelegramId: body.user?.id ?? 0,
                    FirstName: body.user?.first_name ?? "",
                    LastName: body.user?.last_name ?? "",
                    Username: body.user?.username ?? "",
                    IsPremium: body.user?.is_premium ?? false)
                : new SmsVerifyResult(false, Error: body.error ?? "2FA xatosi");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify2FA failed for {Phone}", phone);
            return new SmsVerifyResult(false, Error: ex.Message);
        }
    }

    // ═══ GROUP OPERATIONS ════════════════════════════════════════════════════

    public async Task<GroupJoinResult> JoinGroupsAsync(
        string sessionString, List<string> groupLinks, CancellationToken ct = default)
    {
        if (!IsConfigured) return new GroupJoinResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/join_groups",
                new { session_string = sessionString, groups = groupLinks }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroJoinResponse>(cancellationToken: ct);
            if (body is null) return new GroupJoinResult(false, Error: "Bo'sh javob");

            var results = body.results?.Select(r => new GroupJoinItem(
                r.group ?? "", r.ok, r.error ?? "", r.chat_id, r.title ?? "")).ToList();

            return new GroupJoinResult(body.ok, body.joined, body.total, body.error ?? "", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JoinGroups failed");
            return new GroupJoinResult(false, Error: ex.Message);
        }
    }

    public async Task<BatchSendResult> SendToGroupsAsync(
        string sessionString, List<long> groupIds, string text,
        int delaySeconds = 3, CancellationToken ct = default)
    {
        if (!IsConfigured) return new BatchSendResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/send_to_groups",
                new {
                    session_string = sessionString,
                    groups = groupIds,
                    text,
                    delay_seconds = delaySeconds,
                    add_bot_ad = true
                }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroBatchSendResponse>(cancellationToken: ct);
            if (body is null) return new BatchSendResult(false, Error: "Bo'sh javob");

            var results = body.results?.Select(r => new BatchSendItem(
                r.group, r.ok, r.error ?? "")).ToList();

            return new BatchSendResult(body.ok, body.sent, body.failed, body.total,
                body.spam, body.error ?? "", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendToGroups failed");
            return new BatchSendResult(false, Error: ex.Message);
        }
    }

    // ═══ ANTI-SPAM ═══════════════════════════════════════════════════════════

    public async Task<SpamFixResult> FixSpamAsync(string sessionString, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SpamFixResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/fix_spam",
                new { session_string = sessionString }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroSpamFixResponse>(cancellationToken: ct);
            if (body is null) return new SpamFixResult(false, Error: "Bo'sh javob");

            return new SpamFixResult(body.ok, body.is_premium, body.message ?? "", body.error ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FixSpam failed");
            return new SpamFixResult(false, Error: ex.Message);
        }
    }

    // ═══ SESSION CHECK ═══════════════════════════════════════════════════════

    public async Task<SessionCheckResult> CheckSessionAsync(string sessionString, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SessionCheckResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/check_session",
                new { session_string = sessionString }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroCheckSessionResponse>(cancellationToken: ct);
            if (body is null) return new SessionCheckResult(false, Error: "Bo'sh javob");

            return new SessionCheckResult(true, body.valid, body.is_premium,
                body.user?.id ?? 0, body.error ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckSession failed");
            return new SessionCheckResult(false, Error: ex.Message);
        }
    }

    // ═══ SCRAPING ════════════════════════════════════════════════════════════

    public async Task<ScrapeResult> ScrapeGroupAsync(
        string sessionString, long chatId, int limit = 50, CancellationToken ct = default)
    {
        if (!IsConfigured) return new ScrapeResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/scrape_group",
                new { session_string = sessionString, chat_id = chatId, limit }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroScrapeResponse>(cancellationToken: ct);
            if (body is null) return new ScrapeResult(false, Error: "Bo'sh javob");

            var messages = body.messages?.Select(m => new ScrapedMessage(
                m.id, m.text ?? "",
                DateTime.TryParse(m.date, out var d) ? d : DateTime.UtcNow,
                m.from_id, m.from_name)).ToList();

            return new ScrapeResult(body.ok, body.error ?? "", messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScrapeGroup failed for {ChatId}", chatId);
            return new ScrapeResult(false, Error: ex.Message);
        }
    }

    public async Task<GroupSearchResult> SearchGroupsAsync(
        string sessionString, string query, int limit = 30, CancellationToken ct = default)
    {
        if (!IsConfigured) return new GroupSearchResult(false, Error: "Pyrogram xizmati sozlanmagan");

        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/search_groups",
                new { session_string = sessionString, query, limit }, ct);
            var body = await resp.Content.ReadFromJsonAsync<PyroSearchResponse>(cancellationToken: ct);
            if (body is null) return new GroupSearchResult(false, Error: "Bo'sh javob");

            var groups = body.groups?.Select(g => new FoundGroup(
                g.chat_id, g.title ?? "", g.username ?? "",
                g.members_count, g.type ?? "")).ToList();

            return new GroupSearchResult(body.ok, body.error ?? "", groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchGroups failed for '{Query}'", query);
            return new GroupSearchResult(false, Error: ex.Message);
        }
    }

    // ═══ JSON DTOs ═══════════════════════════════════════════════════════════

    private sealed class PyroSendCodeResponse { public bool ok; public string? phone_code_hash; public string? error; }
    private sealed class PyroVerifyResponse { public bool ok; public bool needs_2fa; public string? session_string; public PyroUser? user; public string? error; }
    private sealed class PyroUser { public long id; public string? first_name; public string? last_name; public string? username; public string? phone; public bool is_premium; }
    private sealed class PyroJoinResponse { public bool ok; public int joined; public int total; public string? error; public List<PyroJoinItem>? results; }
    private sealed class PyroJoinItem { public string? group; public bool ok; public string? error; public long chat_id; public string? title; }
    private sealed class PyroBatchSendResponse { public bool ok; public int sent; public int failed; public int total; public bool spam; public string? error; public List<PyroBatchItem>? results; }
    private sealed class PyroBatchItem { public long group; public bool ok; public string? error; }
    private sealed class PyroSpamFixResponse { public bool ok; public bool is_premium; public string? message; public string? error; }
    private sealed class PyroCheckSessionResponse { public bool ok; public bool valid; public bool is_premium; public PyroUser? user; public string? error; }
    private sealed class PyroScrapeResponse { public bool ok; public string? error; public List<PyroMessage>? messages; }
    private sealed class PyroMessage { public long id; public string? text; public string? date; public long? from_id; public string? from_name; }
    private sealed class PyroSearchResponse { public bool ok; public string? error; public List<PyroGroup>? groups; }
    private sealed class PyroGroup { public long chat_id; public string? title; public string? username; public int members_count; public string? type; }
}
