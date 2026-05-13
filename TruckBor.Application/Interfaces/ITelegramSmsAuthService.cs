namespace TruckBor.Application.Interfaces;

public interface ITelegramSmsAuthService
{
    /// <summary>Send SMS code to phone via Pyrogram microservice.</summary>
    Task<SmsCodeResult> SendCodeAsync(string phone, CancellationToken ct = default);

    /// <summary>Verify the code user received via SMS.</summary>
    Task<SmsVerifyResult> VerifyCodeAsync(string phone, string code, string phoneCodeHash, CancellationToken ct = default);

    /// <summary>Submit 2FA password after code verified.</summary>
    Task<SmsVerifyResult> Verify2FaAsync(string phone, string password, CancellationToken ct = default);
}

public sealed record SmsCodeResult(
    bool   Success,
    string PhoneCodeHash = "",
    string Error         = "");

public sealed record SmsVerifyResult(
    bool   Success,
    bool   Needs2FA = false,
    string Error    = "");
