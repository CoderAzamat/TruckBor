namespace TruckBor.Application.Interfaces;

public interface ITelegramSmsAuthService
{
    /// <summary>Send SMS code to phone via Pyrogram microservice.</summary>
    Task<SmsCodeResult> SendCodeAsync(string phone, CancellationToken ct = default);

    /// <summary>Verify the code user received via SMS.</summary>
    Task<SmsVerifyResult> VerifyCodeAsync(string phone, string code, string phoneCodeHash, CancellationToken ct = default);

    /// <summary>Submit 2FA password after code verified.</summary>
    Task<SmsVerifyResult> Verify2FaAsync(string phone, string password, CancellationToken ct = default);

    /// <summary>Join multiple groups from a user account.</summary>
    Task<GroupJoinResult> JoinGroupsAsync(string sessionString, List<string> groupLinks, CancellationToken ct = default);

    /// <summary>Send message to multiple groups from a user account.</summary>
    Task<BatchSendResult> SendToGroupsAsync(string sessionString, List<long> groupIds, string text, int delaySeconds = 3, CancellationToken ct = default);

    /// <summary>Auto-fix spam via @SpamBot.</summary>
    Task<SpamFixResult> FixSpamAsync(string sessionString, CancellationToken ct = default);

    /// <summary>Check if a session is still valid.</summary>
    Task<SessionCheckResult> CheckSessionAsync(string sessionString, CancellationToken ct = default);

    /// <summary>Read recent messages from a group.</summary>
    Task<ScrapeResult> ScrapeGroupAsync(string sessionString, long chatId, int limit = 50, CancellationToken ct = default);

    /// <summary>Search Telegram for public groups by keyword.</summary>
    Task<GroupSearchResult> SearchGroupsAsync(string sessionString, string query, int limit = 30, CancellationToken ct = default);
}

// ── Auth Results ─────────────────────────────────────────────────────────
public sealed record SmsCodeResult(
    bool   Success,
    string PhoneCodeHash = "",
    string Error         = "");

public sealed record SmsVerifyResult(
    bool   Success,
    bool   Needs2FA      = false,
    string SessionString = "",
    long   TelegramId    = 0,
    string FirstName     = "",
    string LastName      = "",
    string Username      = "",
    bool   IsPremium     = false,
    string Error         = "");

// ── Group Operations ─────────────────────────────────────────────────────
public sealed record GroupJoinResult(
    bool   Success,
    int    Joined  = 0,
    int    Total   = 0,
    string Error   = "",
    List<GroupJoinItem>? Results = null);

public sealed record GroupJoinItem(
    string Group,
    bool   Ok,
    string Error = "",
    long   ChatId = 0,
    string Title  = "");

public sealed record BatchSendResult(
    bool   Success,
    int    Sent    = 0,
    int    Failed  = 0,
    int    Total   = 0,
    bool   Spam    = false,
    string Error   = "",
    List<BatchSendItem>? Results = null);

public sealed record BatchSendItem(
    long   GroupId,
    bool   Ok,
    string Error = "");

public sealed record SpamFixResult(
    bool   Success,
    bool   IsPremium = false,
    string Message   = "",
    string Error     = "");

public sealed record SessionCheckResult(
    bool   Success,
    bool   Valid     = false,
    bool   IsPremium = false,
    long   TelegramId = 0,
    string Error     = "");

// ── Scraping ─────────────────────────────────────────────────────────────
public sealed record ScrapeResult(
    bool   Success,
    string Error = "",
    List<ScrapedMessage>? Messages = null);

public sealed record ScrapedMessage(
    long     Id,
    string   Text,
    DateTime Date,
    long?    FromId,
    string?  FromName);

public sealed record GroupSearchResult(
    bool   Success,
    string Error = "",
    List<FoundGroup>? Groups = null);

public sealed record FoundGroup(
    long   ChatId,
    string Title,
    string Username,
    int    MembersCount,
    string Type);
