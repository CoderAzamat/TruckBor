using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TruckBor.Application.Interfaces;

namespace TruckBor.Infrastructure.Services;

/// <summary>
/// Calls a Pyrogram HTTP microservice for real Telegram SMS authentication.
/// If TelegramAuth:ServiceUrl is empty the service is disabled and every call
/// returns Success=false so callers can gracefully fall back.
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
        _baseUrl = config["TelegramAuth:ServiceUrl"]?.TrimEnd('/');
        _logger  = logger;
    }

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl);

    // ── Send code ─────────────────────────────────────────────────────────
    public async Task<SmsCodeResult> SendCodeAsync(string phone, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsCodeResult(false, Error: "Pyrogram service not configured");

        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/send_code",
                new { phone },
                ct);

            if (!resp.IsSuccessStatusCode)
                return new SmsCodeResult(false, Error: $"HTTP {(int)resp.StatusCode}");

            var body = await resp.Content.ReadFromJsonAsync<PyroSendCodeResponse>(cancellationToken: ct);
            if (body is null) return new SmsCodeResult(false, Error: "Empty response");

            return body.ok
                ? new SmsCodeResult(true, PhoneCodeHash: body.phone_code_hash ?? "")
                : new SmsCodeResult(false, Error: body.error ?? "Unknown error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendCode failed for {Phone}", phone);
            return new SmsCodeResult(false, Error: ex.Message);
        }
    }

    // ── Verify code ───────────────────────────────────────────────────────
    public async Task<SmsVerifyResult> VerifyCodeAsync(
        string phone, string code, string phoneCodeHash, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsVerifyResult(false, Error: "Pyrogram service not configured");

        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/verify_code",
                new { phone, code, phone_code_hash = phoneCodeHash },
                ct);

            var body = await resp.Content.ReadFromJsonAsync<PyroVerifyResponse>(cancellationToken: ct);
            if (body is null) return new SmsVerifyResult(false, Error: "Empty response");

            if (body.ok) return new SmsVerifyResult(true);
            if (body.needs_2fa) return new SmsVerifyResult(false, Needs2FA: true);
            return new SmsVerifyResult(false, Error: body.error ?? "Verification failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyCode failed for {Phone}", phone);
            return new SmsVerifyResult(false, Error: ex.Message);
        }
    }

    // ── Verify 2FA ────────────────────────────────────────────────────────
    public async Task<SmsVerifyResult> Verify2FaAsync(
        string phone, string password, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SmsVerifyResult(false, Error: "Pyrogram service not configured");

        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{_baseUrl}/verify_2fa",
                new { phone, password },
                ct);

            var body = await resp.Content.ReadFromJsonAsync<PyroVerifyResponse>(cancellationToken: ct);
            if (body is null) return new SmsVerifyResult(false, Error: "Empty response");
            return body.ok ? new SmsVerifyResult(true) : new SmsVerifyResult(false, Error: body.error ?? "2FA failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify2FA failed for {Phone}", phone);
            return new SmsVerifyResult(false, Error: ex.Message);
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────
    private sealed class PyroSendCodeResponse
    {
        public bool   ok              { get; set; }
        public string? phone_code_hash { get; set; }
        public string? error           { get; set; }
    }

    private sealed class PyroVerifyResponse
    {
        public bool   ok        { get; set; }
        public bool   needs_2fa { get; set; }
        public string? error    { get; set; }
    }
}
