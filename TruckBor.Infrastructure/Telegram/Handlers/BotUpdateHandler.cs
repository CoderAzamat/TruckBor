using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Telegram.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using KB = TruckBor.Infrastructure.Telegram.Keyboards.Keyboards;

namespace TruckBor.Infrastructure.Telegram.Handlers;

public class BotUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly ITelegramService   _tg;
    private readonly IUserStateService  _state;
    private readonly ILocalizationService _loc;
    private readonly IAppDbContext      _db;
    private readonly ICacheService      _cache;
    private readonly IPostingService    _posting;
    private readonly IConfiguration    _config;
    private readonly ILogger<BotUpdateHandler> _logger;
    private readonly long[]             _adminIds;

    public BotUpdateHandler(
        ITelegramBotClient bot, ITelegramService tg, IUserStateService state,
        ILocalizationService loc, IAppDbContext db, ICacheService cache,
        IPostingService posting, IConfiguration config,
        ILogger<BotUpdateHandler> logger)
    {
        _bot = bot; _tg = tg; _state = state; _loc = loc;
        _db = db; _cache = cache; _posting = posting;
        _config = config; _logger = logger;
        _adminIds = config.GetSection("Bot:AdminIds").Get<long[]>() ?? Array.Empty<long>();
    }

    // ═══ MAIN ENTRY ══════════════════════════════════════════════════════
    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message is { } msg)       await HandleMessageAsync(msg, ct);
            else if (update.CallbackQuery is { } cb) await HandleCallbackAsync(cb, ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "Update xatosi: {Id}", update.Id); }
    }

    // ═══ MESSAGE ═════════════════════════════════════════════════════════
    private async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.From is null) return;
        var tgId   = msg.From.Id;
        var chatId = msg.Chat.Id;
        var isAdmin = _adminIds.Contains(tgId);

        if (await _state.IsFloodingAsync(tgId, ct))
        {
            var fu = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
            await _tg.SendMessageAsync(chatId, _loc.Get("flood_warning", fu?.Language ?? Language.UzLatin), ct: ct);
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);

        if (!isAdmin)
        {
            var maint = await _cache.GetAsync<string>("maintenance_mode", ct) ?? "false";
            if (maint == "true")
            {
                var lang = user?.Language ?? Language.UzLatin;
                await _tg.SendMessageAsync(chatId, _loc.Get("maintenance", lang), ct: ct);
                return;
            }
        }

        var text         = msg.Text?.Trim() ?? string.Empty;
        var currentState = await _state.GetStateAsync(tgId, ct);

        // Active state takes priority
        if (currentState != UserState.None && currentState != UserState.WaitingLanguage)
        {
            var eu = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
            if (eu is not null && !eu.IsOnboarded)
            { await HandleOnboardingAsync(msg, eu, currentState, ct); return; }
        }

        // New user
        if (user is null)
        {
            var welcome =
                "🚛 <b>TruckBor</b> — O'zbekistonning №1 logistika platformasi\n\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "📦 <b>Yuk e'lon</b> — 1000+ guruhga tarqatiladi\n" +
                "🚛 <b>Transport e'lon</b> — Haydovchilar uchun\n" +
                "📮 <b>Dogruz</b> — Qo'shimcha yuk topish\n" +
                "🎯 <b>Mos e'lonlar</b> — AI asosida tanlanadi\n" +
                "💎 <b>VIP obuna</b> — Ko'proq imkoniyatlar\n\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "🌐 <b>1-qadam / 5</b>  ▰▱▱▱▱\n\n" +
                "Tilni tanlang / Выберите язык / Choose language:";
            await _tg.SendMessageAsync(chatId, welcome, KB.LanguageMenu(), ct);
            await _state.SetStateAsync(tgId, UserState.WaitingLanguage, null, ct);
            return;
        }

        if (user.IsBlocked)
        { await _tg.SendMessageAsync(chatId, _loc.Get("blocked", user.Language), ct: ct); return; }

        if (!isAdmin && !await CheckSubscriptionAsync(tgId, chatId, user.Language, ct)) return;

        // Cancel command (by emoji — works for all 5 languages)
        if (text.StartsWith("❌"))
        {
            await _state.ClearStateAsync(tgId, ct);
            var kb = isAdmin ? KB.AdminMenu() : (ReplyMarkup)KB.MainMenu(user.Language);
            await _tg.SendMessageAsync(chatId, _loc.Get("main_menu", user.Language), kb, ct);
            return;
        }

        if (text == "/start") { await HandleStartAsync(tgId, chatId, user, isAdmin, ct); return; }
        if (text == "/admin" && isAdmin) { await _tg.SendMessageAsync(chatId, "👨‍💼 <b>Admin panel</b>", KB.AdminMenu(), ct); return; }

        if (!user.IsOnboarded) { await HandleOnboardingAsync(msg, user, currentState, ct); return; }
        if (currentState != UserState.None) { await HandleStateAsync(msg, user, currentState, isAdmin, ct); return; }
        if (isAdmin && await HandleAdminTextAsync(text, tgId, chatId, ct)) return;
        await HandleUserTextAsync(text, tgId, chatId, user, ct);
    }

    // ═══ START ═══════════════════════════════════════════════════════════
    private async Task HandleStartAsync(long tgId, long chatId,
        Domain.Entities.User user, bool isAdmin, CancellationToken ct)
    {
        await _state.ClearStateAsync(tgId, ct);
        if (!user.IsOnboarded)
        {
            await _tg.SendMessageAsync(chatId, _loc.Get("welcome", user.Language), KB.LanguageMenu(), ct);
            await _state.SetStateAsync(tgId, UserState.WaitingLanguage, null, ct);
            return;
        }
        var kb = isAdmin ? (ReplyMarkup)KB.AdminMenu() : KB.MainMenu(user.Language);
        var greeting = isAdmin
            ? $"👨‍💼 <b>Admin panel</b>\n\nXush kelibsiz, {user.FullName}!"
            : _loc.Get("home_greeting", user.Language, user.FullName, user.Balance,
                HasActiveSub(user.Id, ct).GetAwaiter().GetResult() ? "✅" : "❌");
        await _tg.SendMessageAsync(chatId, greeting, kb, ct);
    }

    private async Task<bool> HasActiveSub(long userId, CancellationToken ct)
        => await _db.Subscriptions.AnyAsync(x => x.UserId == userId &&
            x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);

    // ═══ ONBOARDING ══════════════════════════════════════════════════════
    private async Task HandleOnboardingAsync(Message msg,
        Domain.Entities.User user, UserState state, CancellationToken ct)
    {
        var tgId   = msg.From!.Id;
        var chatId = msg.Chat.Id;
        var text   = msg.Text?.Trim() ?? string.Empty;

        switch (state)
        {
            case UserState.WaitingLanguage: break; // handled via callback
            case UserState.WaitingRole:     break; // handled via callback

            case UserState.WaitingFullName:
                if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
                { await _tg.SendMessageAsync(chatId, _loc.Get("onboard_name_error", user.Language), ct: ct); return; }
                var tu = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
                if (tu is null) return;
                await _state.SetStateAsync(tgId, UserState.WaitingPhone, text, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("onboard_step_phone", tu.Language), KB.RequestContact(tu.Language), ct);
                break;

            case UserState.WaitingPhone:
                var phone    = msg.Contact?.PhoneNumber ?? text;
                var fullName = await _state.GetStateDataAsync<string>(tgId, ct) ?? user.FullName;
                user.FullName    = fullName;
                user.PhoneNumber = phone.StartsWith("+") ? phone : "+" + phone;
                user.IsOnboarded = true;
                await _db.SaveChangesAsync(ct);
                await _state.ClearStateAsync(tgId, ct);
                await _tg.SendMessageAsync(chatId,
                    _loc.Get("onboard_complete", user.Language, fullName, user.PhoneNumber),
                    KB.MainMenu(user.Language), ct);
                break;
        }
    }

    // ═══ STATE MACHINE ════════════════════════════════════════════════════
    private async Task HandleStateAsync(Message msg,
        Domain.Entities.User user, UserState state, bool isAdmin, CancellationToken ct)
    {
        var tgId   = msg.From!.Id;
        var chatId = msg.Chat.Id;
        var text   = msg.Text?.Trim() ?? string.Empty;

        switch (state)
        {
            // ── Payment ──────────────────────────────────────────────────
            case UserState.WaitingPaymentCheck:
                await HandlePaymentCheckAsync(msg, user, ct); break;
            case UserState.WaitingPaymentAmount when isAdmin:
                await HandlePaymentAmountAsync(tgId, chatId, text, ct); break;
            case UserState.WaitingRejectReason when isAdmin:
                await HandleRejectReasonAsync(tgId, chatId, text, ct); break;

            // ── Admin flows ───────────────────────────────────────────────
            case UserState.WaitingBroadcastText when isAdmin:
                await HandleBroadcastAsync(tgId, chatId, text, ct); break;
            case UserState.WaitingUserSearch when isAdmin:
            {
                var meta = await _state.GetStateDataAsync<string>(tgId, ct);
                if (meta == "add_admin") await HandleAddAdminAsync(tgId, chatId, text, ct);
                else                     await HandleUserSearchAsync(chatId, text, ct);
                await _state.ClearStateAsync(tgId, ct);
                break;
            }
            case UserState.WaitingUserMessage when isAdmin:
                await HandleSendUserMessageAsync(tgId, chatId, text, ct); break;
            case UserState.WaitingBalanceAmount when isAdmin:
                await HandleAdminBalanceAsync(tgId, chatId, text, ct); break;
            case UserState.WaitingSettingsValue when isAdmin:
                await HandleSettingsValueAsync(tgId, chatId, text, ct); break;

            case UserState.WaitingCardNumber when isAdmin:
                await _state.SetStateAsync(tgId, UserState.WaitingCardHolder, text, ct);
                await _tg.SendMessageAsync(chatId, "💳 Karta egasining ismini kiriting:", KB.CancelMenu(Language.UzLatin), ct); break;
            case UserState.WaitingCardHolder when isAdmin:
            {
                var cn = await _state.GetStateDataAsync<string>(tgId, ct) ?? "";
                await _state.SetStateAsync(tgId, UserState.WaitingCardBank, $"{cn}|{text}", ct);
                await _tg.SendMessageAsync(chatId, "🏦 Bank nomini kiriting (yoki — deb yozing):", KB.CancelMenu(Language.UzLatin), ct);
                break;
            }
            case UserState.WaitingCardBank when isAdmin:
            {
                var raw    = await _state.GetStateDataAsync<string>(tgId, ct) ?? "";
                var parts  = raw.Split('|');
                var cNum   = parts.Length > 0 ? parts[0] : "";
                var cHolder = parts.Length > 1 ? parts[1] : "";
                var bank   = text == "-" ? null : text;
                await _state.ClearStateAsync(tgId, ct);
                _db.Cards.Add(new Domain.Entities.Card { CardNumber = cNum, CardHolder = cHolder, BankName = bank, IsActive = true });
                await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, $"✅ Karta qo'shildi!\n💳 <code>{cNum}</code>\n👤 {cHolder}", KB.AdminMenu(), ct);
                break;
            }
            case UserState.WaitingChannelId when isAdmin:
                await HandleChannelAddAsync(tgId, chatId, text, ct); break;

            case UserState.WaitingTariffName when isAdmin:
                await _state.SetStateAsync(tgId, UserState.WaitingTariffPrice, text, ct);
                await _tg.SendMessageAsync(chatId, "💰 Tarif narxini kiriting (so'mda):", KB.CancelMenu(Language.UzLatin), ct); break;
            case UserState.WaitingTariffPrice when isAdmin:
            {
                var tName = await _state.GetStateDataAsync<string>(tgId, ct) ?? "";
                if (!decimal.TryParse(text.Replace(" ", ""), out var pr))
                { await _tg.SendMessageAsync(chatId, "❌ Noto'g'ri narx.", ct: ct); break; }
                await _state.SetStateAsync(tgId, UserState.WaitingTariffDays, $"{tName}|{pr}", ct);
                await _tg.SendMessageAsync(chatId, "📅 Muddatini kiriting (kun):", KB.CancelMenu(Language.UzLatin), ct);
                break;
            }
            case UserState.WaitingTariffDays when isAdmin:
            {
                var raw2   = await _state.GetStateDataAsync<string>(tgId, ct) ?? "";
                var sp     = raw2.Split('|');
                var tName2 = sp.Length > 0 ? sp[0] : "Tarif";
                decimal tPrice2 = sp.Length > 1 && decimal.TryParse(sp[1], out var p2) ? p2 : 0;
                if (!int.TryParse(text, out var days)) { await _tg.SendMessageAsync(chatId, "❌ Noto'g'ri kun.", ct: ct); break; }
                await _state.ClearStateAsync(tgId, ct);
                _db.Tariffs.Add(new Domain.Entities.Tariff { Name = tName2, Price = tPrice2, DurationDays = days, MaxAccounts = 3, MaxGroups = 100, PostsPerDay = 5, IsActive = true });
                await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, $"✅ Tarif yaratildi!\n⭐ {tName2} — {tPrice2:N0} so'm / {days} kun", KB.AdminMenu(), ct);
                break;
            }

            // ── Cargo post ──────────────────────────────────────────────
            case UserState.WaitingPostFrom:
            {
                var d = new PostDraft { Type = PostType.Cargo, From = text };
                await _state.SetStateAsync(tgId, UserState.WaitingPostTo, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_to_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingPostTo:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.To = text;
                await _state.SetStateAsync(tgId, UserState.WaitingPostCargoType, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_cargo_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingPostCargoType:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.CargoType = text;
                await _state.SetStateAsync(tgId, UserState.WaitingPostWeight, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_weight_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingPostWeight:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Weight = text;
                await _state.SetStateAsync(tgId, UserState.WaitingPostPrice, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_price_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingPostPrice:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Price = text;
                await _state.SetStateAsync(tgId, UserState.WaitingPostContact, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_contact_prompt", user.Language), KB.RequestContact(user.Language), ct);
                break;
            }
            case UserState.WaitingPostContact:
                await HandlePostCreationAsync(tgId, chatId, msg, user, PostType.Cargo, ct); break;

            // ── Transport post ──────────────────────────────────────────
            case UserState.WaitingTransportFrom:
            {
                var d = new PostDraft { Type = PostType.Transport, From = text };
                await _state.SetStateAsync(tgId, UserState.WaitingTransportTo, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("transport_to_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingTransportTo:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.To = text;
                await _state.SetStateAsync(tgId, UserState.WaitingTransportVehicle, d, ct);
                await _tg.SendMessageAsync(chatId,
                    _loc.Get("transport_vehicle_prompt", user.Language),
                    KB.VehicleTypeMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingTransportVehicle:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.VehicleType = text;
                await _state.SetStateAsync(tgId, UserState.WaitingTransportCapacity, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("transport_capacity_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingTransportCapacity:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Weight = text; // reuse Weight field for capacity
                await _state.SetStateAsync(tgId, UserState.WaitingTransportPrice, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("transport_price_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingTransportPrice:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Price = text;
                await _state.SetStateAsync(tgId, UserState.WaitingTransportPhone, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_contact_prompt", user.Language), KB.RequestContact(user.Language), ct);
                break;
            }
            case UserState.WaitingTransportPhone:
                await HandlePostCreationAsync(tgId, chatId, msg, user, PostType.Transport, ct); break;

            // ── Dogruz post ─────────────────────────────────────────────
            case UserState.WaitingDogruzFrom:
            {
                var d = new PostDraft { Type = PostType.Dogruz, From = text };
                await _state.SetStateAsync(tgId, UserState.WaitingDogruzTo, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("dogruz_to_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingDogruzTo:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.To = text;
                await _state.SetStateAsync(tgId, UserState.WaitingDogruzCapacity, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("dogruz_capacity_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingDogruzCapacity:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Weight = text;
                await _state.SetStateAsync(tgId, UserState.WaitingDogruzPrice, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("dogruz_price_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingDogruzPrice:
            {
                var d = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
                d.Price = text;
                await _state.SetStateAsync(tgId, UserState.WaitingDogruzPhone, d, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("post_contact_prompt", user.Language), KB.RequestContact(user.Language), ct);
                break;
            }
            case UserState.WaitingDogruzPhone:
                await HandlePostCreationAsync(tgId, chatId, msg, user, PostType.Dogruz, ct); break;

            // ── Account ─────────────────────────────────────────────────
            case UserState.WaitingAccountPhone:
                await HandleAccountPhoneAsync(tgId, chatId, msg, user, ct); break;

            // ── Search ──────────────────────────────────────────────────
            case UserState.WaitingSearchFrom:
            {
                await _state.SetStateAsync(tgId, UserState.WaitingSearchTo, text, ct);
                await _tg.SendMessageAsync(chatId, _loc.Get("search_to_prompt", user.Language), KB.CancelMenu(user.Language), ct);
                break;
            }
            case UserState.WaitingSearchTo:
            {
                var from = await _state.GetStateDataAsync<string>(tgId, ct) ?? "";
                await _state.ClearStateAsync(tgId, ct);
                await HandleAdvancedSearchAsync(chatId, user, from, text, ct);
                break;
            }
        }
    }

    // ═══ POST CREATION ════════════════════════════════════════════════════
    private async Task HandlePostCreationAsync(long tgId, long chatId,
        Message msg, Domain.Entities.User user, PostType type, CancellationToken ct)
    {
        var draft = await _state.GetStateDataAsync<PostDraft>(tgId, ct) ?? new PostDraft();
        var phone = msg.Contact?.PhoneNumber ?? msg.Text ?? user.PhoneNumber ?? "";
        if (!phone.StartsWith("+")) phone = "+" + phone;
        await _state.ClearStateAsync(tgId, ct);

        var hasSub = await HasActiveSub(user.Id, ct);
        var post   = new Domain.Entities.Post
        {
            UserId       = user.Id,
            PostType     = type,
            FromCity     = draft.From,
            ToCity       = draft.To,
            CargoType    = draft.CargoType,
            Weight       = draft.Weight,
            VehicleType  = draft.VehicleType,
            Price        = draft.Price,
            ContactPhone = phone,
            PostedBy     = user.Role,
            Status       = PostStatus.Active,
            IsVerified   = hasSub,
            ExpiresAt    = DateTime.UtcNow.AddDays(7),
        };

        _db.Posts.Add(post);
        user.TotalPosts++;
        await _db.SaveChangesAsync(ct);

        await _tg.SendMessageAsync(chatId,
            _loc.Get("post_accepted", user.Language),
            KB.MainMenu(user.Language), ct);

        // Auto-post to channel + groups
        _ = Task.Run(() => _posting.PostToGroupsAsync(post.Id, user.Id, CancellationToken.None));

        // Show matching ads immediately
        await ShowMatchingAdsAsync(chatId, post, user, ct);
    }

    // ═══ AUTO-MATCHING ═══════════════════════════════════════════════════
    private async Task ShowMatchingAdsAsync(long chatId,
        Domain.Entities.Post newPost, Domain.Entities.User user, CancellationToken ct)
    {
        // Opposite type: cargo → show transport, transport → show cargo
        var searchType = newPost.PostType == PostType.Cargo ? PostType.Transport : PostType.Cargo;

        var matches = await _db.Posts
            .Include(x => x.User)
            .Where(x => x.Status == PostStatus.Active &&
                x.ExpiresAt > DateTime.UtcNow &&
                x.PostType == searchType &&
                (x.FromCity.Contains(newPost.FromCity) || newPost.FromCity.Contains(x.FromCity)) &&
                (x.ToCity.Contains(newPost.ToCity)   || newPost.ToCity.Contains(x.ToCity)))
            .OrderByDescending(x => x.IsVerified)
            .ThenByDescending(x => x.CreatedAt)
            .Take(3)
            .ToListAsync(ct);

        if (!matches.Any()) return;

        var hasSub = await HasActiveSub(user.Id, ct);
        var header = _loc.Get("matching_header", user.Language);
        await _tg.SendMessageAsync(chatId, header, ct: ct);

        foreach (var m in matches)
        {
            var card = BuildPostCard(m, user.Language, hasSub);
            await _tg.SendMessageAsync(chatId, card,
                hasSub ? null : KB.ShowPhoneButton(m.Id, user.Language), ct);
        }
    }

    // ═══ USER TEXT HANDLERS ══════════════════════════════════════════════
    private async Task HandleUserTextAsync(string text, long tgId, long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        // Match by emoji prefix — language-independent
        if (text.StartsWith("📦"))  { await HandlePostTypeStartAsync(tgId, chatId, user, PostType.Cargo,     ct); return; }
        if (text.StartsWith("🚛"))  { await HandlePostTypeStartAsync(tgId, chatId, user, PostType.Transport, ct); return; }
        if (text.StartsWith("📮"))  { await HandlePostTypeStartAsync(tgId, chatId, user, PostType.Dogruz,    ct); return; }
        if (text.StartsWith("🎯"))  { await ShowMatchingAdsForUserAsync(chatId, user, ct); return; }
        if (text.StartsWith("🔍"))  { await HandleSearchStartAsync(tgId, chatId, user, ct); return; }
        if (text.StartsWith("💎"))  { await ShowTariffsAsync(chatId, user, ct); return; }
        if (text.StartsWith("📊"))  { await ShowCabinetAsync(chatId, user, ct); return; }
        if (text.StartsWith("📱"))  { await ShowAccountsAsync(chatId, user, ct); return; }
        if (text.StartsWith("📞"))  { await ShowVirtualNumbersAsync(chatId, user, ct); return; }
        if (text.StartsWith("⚙️")) { await ShowUserSettingsAsync(chatId, user, ct); return; }
        if (text.StartsWith("ℹ️")) { await ShowHelpAsync(chatId, user, ct); return; }
        if (!string.IsNullOrEmpty(text) && !text.StartsWith("/"))
            await _tg.SendMessageAsync(chatId, _loc.Get("unknown_cmd", user.Language), ct: ct);
    }

    // ═══ POST TYPE START ══════════════════════════════════════════════════
    private async Task HandlePostTypeStartAsync(long tgId, long chatId,
        Domain.Entities.User user, PostType type, CancellationToken ct)
    {
        var hasSub = await HasActiveSub(user.Id, ct);
        if (!hasSub)
        {
            await _tg.SendMessageAsync(chatId,
                _loc.Get("no_subscription_for_post", user.Language),
                await GetTariffKeyboard(ct), ct);
            return;
        }

        var (state, promptKey) = type switch
        {
            PostType.Transport => (UserState.WaitingTransportFrom, "transport_from_prompt"),
            PostType.Dogruz    => (UserState.WaitingDogruzFrom,    "dogruz_from_prompt"),
            _                  => (UserState.WaitingPostFrom,      "post_from_prompt"),
        };

        var icon = type switch { PostType.Transport => "🚛", PostType.Dogruz => "📮", _ => "📦" };
        var typeName = type switch
        {
            PostType.Transport => _loc.Get("post_type_transport", user.Language),
            PostType.Dogruz    => _loc.Get("post_type_dogruz", user.Language),
            _                  => _loc.Get("post_type_cargo", user.Language),
        };

        await _state.SetStateAsync(tgId, state, null, ct);
        await _tg.SendMessageAsync(chatId,
            $"{icon} <b>{typeName}</b>\n\n{_loc.Get(promptKey, user.Language)}",
            KB.CancelMenu(user.Language), ct);
    }

    // ═══ SEARCH ══════════════════════════════════════════════════════════
    private async Task HandleSearchStartAsync(long tgId, long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        await _state.SetStateAsync(tgId, UserState.WaitingSearchFrom, null, ct);
        await _tg.SendMessageAsync(chatId,
            _loc.Get("search_from_prompt", user.Language),
            KB.CancelMenu(user.Language), ct);
    }

    private async Task HandleAdvancedSearchAsync(long chatId,
        Domain.Entities.User user, string from, string to, CancellationToken ct)
    {
        var hasSub = await HasActiveSub(user.Id, ct);
        var posts  = await _db.Posts
            .Include(x => x.User)
            .Where(x => x.Status == PostStatus.Active && x.ExpiresAt > DateTime.UtcNow &&
                (string.IsNullOrEmpty(from) || x.FromCity.Contains(from)) &&
                (string.IsNullOrEmpty(to)   || x.ToCity.Contains(to)))
            .OrderByDescending(x => x.IsVerified)
            .ThenByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!posts.Any())
        { await _tg.SendMessageAsync(chatId, _loc.Get("no_posts", user.Language), ct: ct); return; }

        await _tg.SendMessageAsync(chatId,
            $"🔍 <b>{from} → {to}</b>\n{_loc.Get("search_title", user.Language)}", ct: ct);

        foreach (var p in posts)
        {
            var card = BuildPostCard(p, user.Language, hasSub);
            await _tg.SendMessageAsync(chatId, card,
                hasSub ? null : KB.ShowPhoneButton(p.Id, user.Language), ct);
        }
    }

    private async Task ShowMatchingAdsForUserAsync(long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        var hasSub = await HasActiveSub(user.Id, ct);

        // Find ads matching the user's typical role
        var searchType = user.Role == UserRole.Driver ? PostType.Cargo : PostType.Transport;

        var posts = await _db.Posts
            .Include(x => x.User)
            .Where(x => x.Status == PostStatus.Active &&
                x.ExpiresAt > DateTime.UtcNow &&
                x.PostType == searchType)
            .OrderByDescending(x => x.IsVerified)
            .ThenByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!posts.Any())
        { await _tg.SendMessageAsync(chatId, _loc.Get("no_posts", user.Language), ct: ct); return; }

        await _tg.SendMessageAsync(chatId, _loc.Get("matching_header", user.Language), ct: ct);

        foreach (var p in posts)
        {
            var card = BuildPostCard(p, user.Language, hasSub);
            await _tg.SendMessageAsync(chatId, card,
                hasSub ? null : KB.ShowPhoneButton(p.Id, user.Language), ct);
        }
    }

    private async Task HandleSearchAsync(long chatId, Domain.Entities.User user, CancellationToken ct)
    {
        var hasSub = await HasActiveSub(user.Id, ct);
        var posts  = await _db.Posts
            .Include(x => x.User)
            .Where(x => x.Status == PostStatus.Active && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.IsVerified)
            .ThenByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (!posts.Any()) { await _tg.SendMessageAsync(chatId, _loc.Get("no_posts", user.Language), ct: ct); return; }
        await _tg.SendMessageAsync(chatId, _loc.Get("search_title", user.Language), ct: ct);
        foreach (var p in posts)
        {
            var card = BuildPostCard(p, user.Language, hasSub);
            await _tg.SendMessageAsync(chatId, card, hasSub ? null : KB.ShowPhoneButton(p.Id, user.Language), ct);
        }
    }

    // ═══ POST CARD BUILDER ════════════════════════════════════════════════
    private static string BuildPostCard(Domain.Entities.Post post, Language lang, bool showPhone)
    {
        var icon   = post.PostType switch { PostType.Transport => "🚛", PostType.Dogruz => "📮", _ => "📦" };
        var verify = post.IsVerified ? " ✅" : "";
        var lines  = new System.Text.StringBuilder();

        lines.AppendLine($"{icon} <b>{post.FromCity?.ToUpper()} → {post.ToCity?.ToUpper()}</b>{verify}");
        lines.AppendLine("━━━━━━━━━━━━━━━━━━━");

        if (post.PostType == PostType.Cargo)
        {
            if (!string.IsNullOrEmpty(post.CargoType)) lines.AppendLine($"🏷 {post.CargoType}");
            if (!string.IsNullOrEmpty(post.Weight))    lines.AppendLine($"⚖️ {post.Weight}");
        }
        else if (post.PostType == PostType.Transport)
        {
            if (!string.IsNullOrEmpty(post.VehicleType)) lines.AppendLine($"🚗 {post.VehicleType}");
            if (!string.IsNullOrEmpty(post.Weight))      lines.AppendLine($"⚖️ {post.Weight}");
        }
        else // Dogruz
        {
            lines.AppendLine("📮 Dogruz (bo'sh joy bor)");
            if (!string.IsNullOrEmpty(post.Weight)) lines.AppendLine($"📐 Bo'sh: {post.Weight}");
        }

        if (!string.IsNullOrEmpty(post.Price)) lines.AppendLine($"💰 {post.Price}");

        var phone = showPhone
            ? $"📞 <code>{post.ContactPhone}</code>"
            : $"📞 {MaskPhone(post.ContactPhone)}  🔒 <i>VIP</i>";
        lines.AppendLine(phone);

        if (post.User is not null)
            lines.AppendLine($"👤 {post.User.FullName}");

        lines.AppendLine($"🕐 {post.CreatedAt:dd.MM.yyyy HH:mm}");
        return lines.ToString().TrimEnd();
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return "—";
        if (phone.Length < 5) return phone;
        // Show: +998 ** *** **XX
        var last2 = phone[^2..];
        return $"+998 ** *** **{last2}";
    }

    // ═══ TARIFFS ═════════════════════════════════════════════════════════
    private async Task ShowTariffsAsync(long chatId, Domain.Entities.User user, CancellationToken ct)
    {
        var tariffs = await _db.Tariffs
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        if (!tariffs.Any()) { await _tg.SendMessageAsync(chatId, "Hozircha tariflar yo'q.", ct: ct); return; }

        var lang = user.Language;
        var sb   = new System.Text.StringBuilder();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("⭐ <b>TruckBor VIP Tariflar</b>");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━\n");

        foreach (var t in tariffs)
        {
            var badge = t.IsRecommended ? "🔥 <b>TAVSIYA ETILADI</b>\n" : "";
            sb.AppendLine($"{badge}{'⭐'} <b>{t.Name}</b>");
            if (!string.IsNullOrEmpty(t.Description)) sb.AppendLine($"   {t.Description}");
            sb.AppendLine($"   👥 {t.MaxAccounts} akkaunt  |  🌐 {t.MaxGroups} guruh");
            sb.AppendLine($"   📤 Kuniga {t.PostsPerDay} e'lon");
            sb.AppendLine($"   💰 <b>{t.Price:N0}</b> so'm/oy\n");
        }

        sb.AppendLine("✅ <b>Afzalliklar:</b>");
        sb.AppendLine("• 📞 To'liq telefon raqamini ko'rish");
        sb.AppendLine("• ✅ Tasdiqlangan profil belgisi");
        sb.AppendLine("• 🚀 1000+ guruhga e'lon tarqatish");
        sb.AppendLine("• 🎯 Mos e'lonlar tanlash");

        var kb = KB.TariffList(tariffs.Select(t => (t.Id, t.Name, t.Price, t.IsRecommended)));
        await _tg.SendMessageAsync(chatId, sb.ToString(), kb, ct);
    }

    // ═══ CABINET ═════════════════════════════════════════════════════════
    private async Task ShowCabinetAsync(long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        var sub = await _db.Subscriptions
            .Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);

        var text =
            "━━━━━━━━━━━━━━━━━━━━━━\n" +
            "📊 <b>MENING KABINETIM</b>\n" +
            "━━━━━━━━━━━━━━━━━━━━━━\n\n" +
            $"👤 <b>{user.FullName}</b>{(user.IsPremium ? " 💎" : "")}\n" +
            $"📱 {user.PhoneNumber}\n" +
            $"🆔 <code>{user.TelegramId}</code>\n" +
            $"🎭 {RoleName(user.Role)}\n\n" +
            $"💰 Balans: <b>{user.Balance:N0}</b> so'm\n" +
            $"📦 Jami e'lonlar: <b>{user.TotalPosts}</b>\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━\n" +
            (sub is not null
                ? $"⭐ Tarif: <b>{sub.Tariff?.Name}</b>\n" +
                  $"📅 Tugash: <b>{sub.EndDate:dd.MM.yyyy}</b>\n" +
                  $"⏰ Qoldi: <b>{sub.DaysLeft}</b> kun"
                : "⭐ Tarif: <b>Yo'q</b> — VIP olish uchun tarifni tanlang!");

        await _tg.SendMessageAsync(chatId, text, KB.MyCabinetMenu(user.Language), ct);
    }

    private static string RoleName(UserRole role) => role switch
    {
        UserRole.Driver     => "🚛 Haydovchi",
        UserRole.CargoOwner => "📦 Yuk egasi",
        _                   => "🧭 Logist",
    };

    // ═══ ACCOUNTS ════════════════════════════════════════════════════════
    private async Task ShowAccountsAsync(long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        var accounts = await _db.TelegramAccounts
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.IsActive)
            .ToListAsync(ct);

        var sub = await _db.Subscriptions
            .Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);

        var maxAccounts = sub?.Tariff?.MaxAccounts ?? 0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📱 <b>Akkauntlarim ({accounts.Count}/{maxAccounts})</b>\n");

        if (!accounts.Any())
        {
            sb.AppendLine("Hozircha akkaunt yo'q.");
            sb.AppendLine("\n💡 Akkaunt qo'shish uchun VIP obuna kerak!");
        }
        else
        {
            foreach (var acc in accounts)
            {
                var st = acc.IsSpammed ? "🚫 Spam" : acc.IsActive ? "✅ Faol" : "❌ Nofaol";
                sb.AppendLine($"📞 {acc.PhoneNumber}");
                sb.AppendLine($"   {st}{(acc.IsPremium ? " | 💎" : "")}");
                sb.AppendLine($"   📤 Yuborildi: {acc.PostsSent}\n");
            }
        }

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var acc in accounts)
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"🗑 {acc.PhoneNumber}", $"account:delete:{acc.Id}")
            });

        if (maxAccounts > 0 && accounts.Count < maxAccounts)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Akkaunt qo'shish", "account:add") });

        var kb = rows.Any() ? new InlineKeyboardMarkup(rows) : null;
        await _tg.SendMessageAsync(chatId, sb.ToString(), kb, ct);
    }

    private async Task ShowVirtualNumbersAsync(long chatId,
        Domain.Entities.User user, CancellationToken ct)
    {
        var text =
            "📞 <b>Virtual Raqamlar</b>\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━\n" +
            "SMS qabul qilish uchun virtual raqam oling.\n\n" +
            "💡 <b>Nima uchun kerak?</b>\n" +
            "• Telegram akkaunt ro'yxatdan o'tkazish\n" +
            "• Spam himoyasidan o'tish\n" +
            "• Bir nechta akkaunt boshqarish\n\n" +
            "🌍 <b>Mavjud mamlakatlar:</b>\n" +
            "🇺🇿 O'zbekiston — 500 so'm/SMS\n" +
            "🇷🇺 Rossiya — 300 so'm/SMS\n" +
            "🇰🇿 Qozog'iston — 400 so'm/SMS\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━\n" +
            "💰 Balans: <b>" + $"{user.Balance:N0}</b> so'm\n\n" +
            "⚡ Tez orada ishga tushadi!";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🛒 Raqam sotib olish", "vnumber:buy") },
            new[] { InlineKeyboardButton.WithCallbackData("📋 Mening raqamlarim", "vnumber:list") }
        });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task ShowUserSettingsAsync(long chatId, Domain.Entities.User user, CancellationToken ct)
    {
        var text = _loc.Get("settings_text", user.Language, _loc.Get("language_name", user.Language));
        var kb   = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(
                _loc.Get("settings_change_lang", user.Language), "settings:language") }
        });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task ShowHelpAsync(long chatId, Domain.Entities.User user, CancellationToken ct)
        => await _tg.SendMessageAsync(chatId, _loc.Get("help_text", user.Language), ct: ct);

    // ═══ PAYMENT ═════════════════════════════════════════════════════════
    private async Task HandlePaymentCheckAsync(Message msg,
        Domain.Entities.User user, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var tgId   = msg.From!.Id;

        string? fileId = null;
        string? fileType = null;

        if (msg.Photo is { Length: > 0 }) { fileId = msg.Photo.Last().FileId; fileType = "photo"; }
        else if (msg.Document != null)    { fileId = msg.Document.FileId;     fileType = "document"; }
        else
        {
            await _tg.SendMessageAsync(chatId,
                "📸 Chekni <b>rasm</b> yoki <b>fayl</b> sifatida yuboring.", ct: ct);
            return;
        }

        var tariffIdStr = await _state.GetStateDataAsync<string>(tgId, ct);
        long? tariffId  = long.TryParse(tariffIdStr, out var pid) ? pid : null;
        Domain.Entities.Tariff? tariff = null;
        if (tariffId.HasValue)
            tariff = await _db.Tariffs.FirstOrDefaultAsync(x => x.Id == tariffId, ct);

        var payment = new Domain.Entities.Payment
        {
            UserId      = user.Id,
            TariffId    = tariffId,
            Amount      = tariff?.Price ?? 0,
            Type        = PaymentType.Manual,
            Status      = PaymentStatus.Pending,
            CheckFileId = fileId,
            Comment     = $"FileType:{fileType}"
        };
        await _db.Payments.AddAsync(payment, ct);
        await _db.SaveChangesAsync(ct);
        await _state.ClearStateAsync(tgId, ct);

        await _tg.SendMessageAsync(chatId,
            "✅ <b>Chekingiz qabul qilindi!</b>\n\n" +
            "⏳ Admin tekshirib, 5–30 daqiqa ichida balans to'ldiriladi.\n\n" +
            "📞 Savollar: @TruckBorAdmin",
            KB.MainMenu(user.Language), ct);

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); }
        catch { tz = TimeZoneInfo.Utc; }
        var tashkent = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        var adminMsg =
            $"💰 <b>YANGI TO'LOV #{payment.Id}</b>\n\n" +
            $"━━━━━━━━━━━━━━━━━━━\n" +
            $"👤 {user.FullName}\n" +
            $"📱 {user.PhoneNumber}\n" +
            $"🆔 TG: <code>{user.TelegramId}</code>\n" +
            (tariff is not null ? $"⭐ Tarif: <b>{tariff.Name}</b> — {tariff.Price:N0} so'm\n" : "") +
            $"🕐 {tashkent:dd.MM.yyyy HH:mm:ss}\n" +
            $"━━━━━━━━━━━━━━━━━━━";

        var kb = KB.PaymentConfirmation(payment.Id);
        foreach (var adminId in _adminIds)
        {
            try
            {
                if (fileType == "photo")
                    await _bot.SendPhoto(adminId, fileId!, caption: adminMsg, parseMode: ParseMode.Html, replyMarkup: kb);
                else
                    await _bot.SendDocument(adminId, fileId!, caption: adminMsg, parseMode: ParseMode.Html, replyMarkup: kb);
            }
            catch (Exception ex) { _logger.LogError(ex, "Admin {Id} ga yuborishda xato", adminId); }
        }
    }

    private async Task HandlePaymentAmountAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        var clean = text.Replace(" ", "").Replace(",", "");
        if (!decimal.TryParse(clean, out var amount) || amount <= 0)
        { await _tg.SendMessageAsync(chatId, "❌ Noto'g'ri summa. Masalan: <code>99000</code>", ct: ct); return; }

        var pidStr = await _state.GetStateDataAsync<string>(adminId, ct);
        if (!long.TryParse(pidStr, out var paymentId)) return;

        var payment = await _db.Payments.Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null) return;

        payment.Amount           = amount;
        payment.Status           = PaymentStatus.Approved;
        payment.ApprovedByAdminId = adminId;
        payment.ApprovedAt       = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user != null)
        {
            user.Balance += amount;
            if (payment.Tariff is not null)
            {
                var ex = await _db.Subscriptions
                    .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                        x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);
                if (ex is not null) ex.EndDate = ex.EndDate.AddDays(payment.Tariff.DurationDays);
                else _db.Subscriptions.Add(new Domain.Entities.Subscription
                {
                    UserId = user.Id, TariffId = payment.Tariff.Id,
                    Status = SubscriptionStatus.Active, StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(payment.Tariff.DurationDays),
                });
            }
            await _db.SaveChangesAsync(ct);
            try
            {
                await _tg.SendMessageAsync(user.TelegramId,
                    $"✅ <b>Balansingiz to'ldirildi!</b>\n\n" +
                    $"💰 Qo'shildi: <b>{amount:N0}</b> so'm\n" +
                    $"💳 Joriy balans: <b>{user.Balance:N0}</b> so'm" +
                    (payment.Tariff is not null ? $"\n\n⭐ <b>{payment.Tariff.Name}</b> tarifi faollashtirildi!\n📅 {payment.Tariff.DurationDays} kun" : ""),
                    KB.MainMenu(user.Language), ct);
            }
            catch { }
        }
        else { await _db.SaveChangesAsync(ct); }

        await _state.ClearStateAsync(adminId, ct);
        await _tg.SendMessageAsync(chatId,
            $"✅ To'lov tasdiqlandi!\n💰 {amount:N0} so'm" +
            (payment.Tariff is not null ? $"\n⭐ {payment.Tariff.Name}" : ""),
            KB.AdminMenu(), ct);
    }

    private async Task HandleRejectReasonAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        var pidStr = await _state.GetStateDataAsync<string>(adminId, ct);
        if (!long.TryParse(pidStr, out var paymentId)) return;
        var reason  = text.Trim() == "-" ? "Admin tomonidan rad etildi" : text.Trim();
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null) return;
        payment.Status = PaymentStatus.Rejected;
        payment.RejectionReason = reason;
        payment.ApprovedByAdminId = adminId;
        await _db.SaveChangesAsync(ct);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == payment.UserId, ct);
        if (user != null) try { await _tg.SendMessageAsync(user.TelegramId, $"❌ <b>To'lovingiz rad etildi.</b>\n\nSabab: {reason}\n\n📞 @TruckBorAdmin", ct: ct); } catch { }
        await _state.ClearStateAsync(adminId, ct);
        await _tg.SendMessageAsync(chatId, $"❌ To'lov rad etildi.\nSabab: {reason}", KB.AdminMenu(), ct);
    }

    // ═══ ADMIN TEXT ═══════════════════════════════════════════════════════
    private async Task<bool> HandleAdminTextAsync(string text, long tgId, long chatId, CancellationToken ct)
    {
        switch (text)
        {
            case "📊 Statistika":          await AdminStatisticsAsync(chatId, ct);           return true;
            case "👥 Foydalanuvchilar":     await AdminUsersAsync(chatId, ct);                return true;
            case "💰 To'lovlar":           await AdminPaymentsAsync(chatId, ct);             return true;
            case "⭐ Tariflar":             await AdminTariffsAsync(chatId, ct);              return true;
            case "📢 Kanallar":            await AdminChannelsAsync(chatId, ct);             return true;
            case "💳 Kartalar":            await AdminCardsAsync(chatId, ct);                return true;
            case "📱 Akkauntlar":          await AdminAccountsAsync(chatId, ct);             return true;
            case "🌐 Guruhlar":            await AdminGroupsAsync(chatId, ct);               return true;
            case "📬 Broadcast":           await AdminBroadcastStartAsync(tgId, chatId, ct); return true;
            case "⚙️ Sozlamalar":         await AdminSettingsAsync(chatId, ct);             return true;
            case "🔒 Maj.obuna":           await AdminMandatoryAsync(chatId, ct);            return true;
            case "🗑 Tozalash":
                await _tg.SendMessageAsync(chatId,
                    "🗑 <b>Ma'lumotlarni tozalash</b>\n\n⚠️ Bu amal qaytarib bo'lmaydi!",
                    KB.ClearDataMenu(), ct);
                return true;
            case "👑 Adminlar":            await AdminsListAsync(chatId, ct);                return true;
            case "🌐 Web panel":
                await _tg.SendMessageAsync(chatId, "🌐 <b>Web Admin Panel</b>\n\nhttps://admin.truckbor.uz", ct: ct);
                return true;
            case "🏠 Asosiy menyu":
                await _tg.SendMessageAsync(chatId, "👨‍💼 <b>Admin panel</b>", KB.AdminMenu(), ct);
                return true;
            default: return false;
        }
    }

    // ═══ ADMIN FUNCTIONS ════════════════════════════════════════════════
    private async Task AdminStatisticsAsync(long chatId, CancellationToken ct)
    {
        var totalUsers    = await _db.Users.CountAsync(ct);
        var todayUsers    = await _db.Users.CountAsync(x => x.CreatedAt.Date == DateTime.UtcNow.Date, ct);
        var activeSubs    = await _db.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);
        var pendingPay    = await _db.Payments.CountAsync(x => x.Status == PaymentStatus.Pending, ct);
        var totalRevenue  = await _db.Payments.Where(x => x.Status == PaymentStatus.Approved).SumAsync(x => x.Amount, ct);
        var todayRevenue  = await _db.Payments.Where(x => x.Status == PaymentStatus.Approved && x.ApprovedAt!.Value.Date == DateTime.UtcNow.Date).SumAsync(x => x.Amount, ct);
        var totalPosts    = await _db.Posts.CountAsync(ct);
        var todayPosts    = await _db.Posts.CountAsync(x => x.CreatedAt.Date == DateTime.UtcNow.Date, ct);
        var totalGroups   = await _db.Groups.CountAsync(ct);
        var activeGroups  = await _db.Groups.CountAsync(x => x.IsActive, ct);

        TimeZoneInfo tz; try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); } catch { tz = TimeZoneInfo.Utc; }
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        var text =
            $"📊 <b>Bot Statistikasi</b>\n🕐 {now:dd.MM.yyyy HH:mm}\n\n" +
            $"━━━━ 👥 FOYDALANUVCHILAR ━━━━\n" +
            $"• Jami: <b>{totalUsers:N0}</b> ta\n• Bugun: <b>+{todayUsers}</b> ta\n• Faol obunalar: <b>{activeSubs}</b> ta\n\n" +
            $"━━━━ 💰 MOLIYA ━━━━\n" +
            $"• Bugungi: <b>{todayRevenue:N0}</b> so'm\n• Jami: <b>{totalRevenue:N0}</b> so'm\n• Kutilayotgan to'lov: <b>{pendingPay}</b> ta\n\n" +
            $"━━━━ 📦 E'LONLAR ━━━━\n" +
            $"• Bugun: <b>{todayPosts}</b> ta\n• Jami: <b>{totalPosts:N0}</b> ta\n\n" +
            $"━━━━ 🌐 GURUHLAR ━━━━\n" +
            $"• Jami: <b>{totalGroups:N0}</b> ta | Faol: <b>{activeGroups:N0}</b> ta";

        await _tg.SendMessageAsync(chatId, text, ct: ct);
    }

    private async Task AdminUsersAsync(long chatId, CancellationToken ct)
    {
        var total = await _db.Users.CountAsync(ct);
        var users = await _db.Users.OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
        var text  = $"👥 <b>Foydalanuvchilar</b> (jami: {total:N0})\n\n";
        foreach (var u in users)
            text += $"• {u.FullName} | {u.Balance:N0} so'm | {u.CreatedAt:dd.MM}\n";
        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Qidirish", "admin:search_user"),
                InlineKeyboardButton.WithCallbackData("📊 Export",   "admin:pdf_users")
            }
        });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task AdminPaymentsAsync(long chatId, CancellationToken ct)
    {
        var payments = await _db.Payments.Include(x => x.User)
            .Where(x => x.Status == PaymentStatus.Pending)
            .OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);

        if (!payments.Any()) { await _tg.SendMessageAsync(chatId, "✅ Kutilayotgan to'lovlar yo'q.", ct: ct); return; }
        await _tg.SendMessageAsync(chatId, $"💰 <b>Kutilayotgan: {payments.Count} ta</b>", ct: ct);

        foreach (var p in payments)
        {
            TimeZoneInfo tz; try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent"); } catch { tz = TimeZoneInfo.Utc; }
            var time = TimeZoneInfo.ConvertTimeFromUtc(p.CreatedAt, tz);
            var txt  = $"💳 <b>To'lov #{p.Id}</b>\n👤 {p.User?.FullName}\n📱 {p.User?.PhoneNumber}\n🕐 {time:dd.MM.yyyy HH:mm}";
            if (!string.IsNullOrEmpty(p.CheckFileId))
            {
                try { await _bot.SendPhoto(chatId, p.CheckFileId, caption: txt, parseMode: ParseMode.Html, replyMarkup: KB.PaymentConfirmation(p.Id)); continue; }
                catch { }
            }
            await _tg.SendMessageAsync(chatId, txt, KB.PaymentConfirmation(p.Id), ct);
        }
    }

    private async Task AdminTariffsAsync(long chatId, CancellationToken ct)
    {
        var tariffs = await _db.Tariffs.OrderBy(x => x.SortOrder).ToListAsync(ct);
        var text    = $"⭐ <b>Tariflar ({tariffs.Count} ta)</b>\n\n";
        foreach (var t in tariffs)
            text += $"{(t.IsActive ? "✅" : "❌")} {t.Name} — {t.Price:N0} so'm / {t.DurationDays} kun\n";
        if (!tariffs.Any()) text += "Tariflar yo'q.\n";
        var kb = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("➕ Tarif qo'shish", "tariff:add") } });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task AdminChannelsAsync(long chatId, CancellationToken ct)
    {
        var channels = await _db.Channels.ToListAsync(ct);
        var text     = channels.Any()
            ? $"📢 <b>Kanallar ({channels.Count})</b>\n\n" + string.Join("\n", channels.Select(c => $"{(c.IsActive ? "✅" : "❌")} {c.Title}"))
            : "📢 Kanallar yo'q.";
        await _tg.SendMessageAsync(chatId, text, KB.MandatoryMenu(channels.Any(c => c.IsActive)), ct);
    }

    private async Task AdminCardsAsync(long chatId, CancellationToken ct)
    {
        var cards = await _db.Cards.ToListAsync(ct);
        var text  = cards.Any()
            ? $"💳 <b>Kartalar ({cards.Count})</b>\n\n" + string.Join("\n\n", cards.Select(c => $"{(c.IsActive ? "✅" : "❌")} <code>{c.CardNumber}</code>\n👤 {c.CardHolder}"))
            : "💳 Kartalar yo'q.";
        var kb = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("➕ Karta qo'shish", "card:add") } });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task AdminAccountsAsync(long chatId, CancellationToken ct)
    {
        var total   = await _db.TelegramAccounts.CountAsync(ct);
        var active  = await _db.TelegramAccounts.CountAsync(x => x.IsActive, ct);
        var spammed = await _db.TelegramAccounts.CountAsync(x => x.IsSpammed, ct);
        await _tg.SendMessageAsync(chatId,
            $"📱 <b>Akkauntlar</b>\n• Jami: <b>{total}</b>\n• Faol: <b>{active}</b>\n• Spam: <b>{spammed}</b>", ct: ct);
    }

    private async Task AdminGroupsAsync(long chatId, CancellationToken ct)
    {
        var total  = await _db.Groups.CountAsync(ct);
        var active = await _db.Groups.CountAsync(x => x.IsActive, ct);
        await _tg.SendMessageAsync(chatId,
            $"🌐 <b>Guruhlar bazasi</b>\n• Jami: <b>{total:N0}</b>\n• Faol: <b>{active:N0}</b>\n\n🌐 Boshqarish: admin.truckbor.uz", ct: ct);
    }

    private async Task AdminBroadcastStartAsync(long tgId, long chatId, CancellationToken ct)
    {
        var total = await _db.Users.CountAsync(ct);
        await _state.SetStateAsync(tgId, UserState.WaitingBroadcastText, null, ct);
        await _tg.SendMessageAsync(chatId,
            $"📬 <b>Broadcast</b>\n\n👥 {total:N0} ta foydalanuvchiga yuboriladi.\n\nXabar matnini yozing:",
            KB.CancelMenu(Language.UzLatin), ct);
    }

    private async Task HandleBroadcastAsync(long tgId, long chatId, string text, CancellationToken ct)
    {
        var total = await _db.Users.CountAsync(ct);
        await _state.SetStateAsync(tgId, UserState.WaitingBroadcastText, text, ct);
        var kb = KB.Confirm("broadcast:send", "📬 Ha, yuborish");
        await _tg.SendMessageAsync(chatId,
            $"📬 <b>Ko'rinishi:</b>\n\n{text}\n\n━━━━━━━━━━━━━━━━━\n📨 {total:N0} ta foydalanuvchiga\n\nTasdiqlaysizmi?", kb, ct);
    }

    private async Task AdminSettingsAsync(long chatId, CancellationToken ct)
        => await _tg.SendMessageAsync(chatId, "⚙️ <b>Sozlamalar</b>\n\nO'zgartirish uchun tugmani bosing:", KB.SettingsMenu(), ct);

    private async Task AdminMandatoryAsync(long chatId, CancellationToken ct)
    {
        var channels = await _db.Channels.Where(x => x.IsRequired).ToListAsync(ct);
        var text =
            $"🔒 <b>Majburiy obuna</b>\n\n" +
            $"Holat: {(channels.Any(x => x.IsActive) ? "🟢 Yoqilgan" : "🔴 O'chirilgan")}\n\n" +
            (channels.Any() ? string.Join("\n", channels.Select(c => $"• {c.Title} — {(c.IsActive ? "✅" : "❌")}")) : "Kanallar yo'q");
        await _tg.SendMessageAsync(chatId, text, KB.MandatoryMenu(channels.Any(x => x.IsActive)), ct);
    }

    private async Task AdminsListAsync(long chatId, CancellationToken ct)
    {
        var admins = await _db.AdminUsers.ToListAsync(ct);
        var text =
            $"👑 <b>Adminlar</b>\n\n" +
            $"🔱 Super: {string.Join(", ", _adminIds.Select(id => $"<code>{id}</code>"))}\n\n" +
            (admins.Any() ? string.Join("\n", admins.Select(a => $"• {a.FullName} — {(a.IsSuper ? "👑" : "👨‍💼")} <code>{a.TelegramId}</code>")) : "");
        var kb = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData("➕ Admin qo'shish", "admin:add") } });
        await _tg.SendMessageAsync(chatId, text, kb, ct);
    }

    private async Task HandleUserSearchAsync(long chatId, string query, CancellationToken ct)
    {
        var users = await _db.Users
            .Where(x => x.FullName.Contains(query) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(query)) ||
                x.TelegramId.ToString().Contains(query))
            .Take(5).ToListAsync(ct);

        if (!users.Any()) { await _tg.SendMessageAsync(chatId, "❌ Foydalanuvchi topilmadi.", KB.AdminMenu(), ct); return; }

        foreach (var u in users)
        {
            var sub = await _db.Subscriptions.Include(x => x.Tariff)
                .FirstOrDefaultAsync(x => x.UserId == u.Id && x.Status == SubscriptionStatus.Active, ct);
            var text =
                $"👤 <b>{u.FullName}</b>\n📱 {u.PhoneNumber}\n🆔 <code>{u.TelegramId}</code>\n" +
                $"💰 {u.Balance:N0} so'm\n📦 {u.TotalPosts} e'lon\n" +
                (sub is not null ? $"⭐ {sub.Tariff?.Name} ({sub.DaysLeft} kun)\n" : "⭐ Yo'q\n") +
                $"{(u.IsBlocked ? "🚫 Bloklangan" : "✅ Faol")} | {u.CreatedAt:dd.MM.yyyy}";
            await _tg.SendMessageAsync(chatId, text, KB.UserActions(u.TelegramId, u.IsBlocked), ct);
        }
    }

    private async Task HandleAddAdminAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        if (!long.TryParse(text.Trim(), out var newTgId))
        { await _tg.SendMessageAsync(chatId, "❌ Noto'g'ri Telegram ID. Masalan: <code>123456789</code>", KB.AdminMenu(), ct); return; }
        if (await _db.AdminUsers.AnyAsync(x => x.TelegramId == newTgId, ct))
        { await _tg.SendMessageAsync(chatId, "⚠️ Bu foydalanuvchi allaqachon admin.", KB.AdminMenu(), ct); return; }
        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == newTgId, ct);
        _db.AdminUsers.Add(new Domain.Entities.AdminUser
        {
            TelegramId = newTgId, FullName = user?.FullName ?? $"Admin {newTgId}",
            CanManageUsers = true, CanManagePayments = true, CanViewStatistics = true,
        });
        await _db.SaveChangesAsync(ct);
        try { await _tg.SendMessageAsync(newTgId, "👑 Siz admin etib tayinlandingiz!", ct: ct); } catch { }
        await _tg.SendMessageAsync(chatId, $"✅ Admin qo'shildi: <code>{newTgId}</code>", KB.AdminMenu(), ct);
    }

    private async Task HandleSendUserMessageAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        var uid = await _state.GetStateDataAsync<string>(adminId, ct);
        if (!long.TryParse(uid, out var userTgId)) return;
        await _state.ClearStateAsync(adminId, ct);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == userTgId, ct);
        if (user is null) return;
        try
        {
            await _tg.SendMessageAsync(user.TelegramId, $"📢 <b>Admin xabari:</b>\n\n{text}", ct: ct);
            await _tg.SendMessageAsync(chatId, $"✅ {user.FullName} ga xabar yuborildi.", KB.AdminMenu(), ct);
        }
        catch { await _tg.SendMessageAsync(chatId, "❌ Xabar yuborib bo'lmadi.", KB.AdminMenu(), ct); }
    }

    private async Task HandleAdminBalanceAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        var clean = text.Replace(" ", "").Replace(",", "");
        if (!decimal.TryParse(clean, out var amount)) return;
        var uid = await _state.GetStateDataAsync<string>(adminId, ct);
        if (!long.TryParse(uid, out var userId)) return;
        await _state.ClearStateAsync(adminId, ct);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return;
        user.Balance += amount;
        await _db.SaveChangesAsync(ct);
        var sign = amount >= 0 ? $"+{amount:N0}" : $"{amount:N0}";
        try { await _tg.SendMessageAsync(user.TelegramId, $"💰 Balans yangilandi: {sign} so'm\nJoriy: <b>{user.Balance:N0}</b> so'm", ct: ct); } catch { }
        await _tg.SendMessageAsync(chatId, $"✅ Balans yangilandi: {sign} so'm", KB.AdminMenu(), ct);
    }

    private async Task HandleSettingsValueAsync(long adminId, long chatId, string text, CancellationToken ct)
    {
        var key = await _state.GetStateDataAsync<string>(adminId, ct);
        await _state.ClearStateAsync(adminId, ct);
        if (string.IsNullOrEmpty(key)) return;
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (setting is null) { setting = new Domain.Entities.Setting { Key = key, Value = text }; await _db.Settings.AddAsync(setting, ct); }
        else setting.Value = text;
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(key, ct);
        await _cache.SetAsync(key, text, ct: ct);
        await _tg.SendMessageAsync(chatId, $"✅ Sozlama saqlandi!\n🔑 {key}: {text}", KB.AdminMenu(), ct);
    }

    private async Task HandleChannelAddAsync(long tgId, long chatId, string text, CancellationToken ct)
    {
        await _state.ClearStateAsync(tgId, ct);
        if (!long.TryParse(text.Trim(), out var channelId))
        { await _tg.SendMessageAsync(chatId, "❌ Noto'g'ri ID. Masalan: <code>-1001234567890</code>", KB.CancelMenu(Language.UzLatin), ct); return; }
        try
        {
            var chat = await _bot.GetChat(channelId, ct);
            _db.Channels.Add(new Domain.Entities.Channel { TelegramChannelId = channelId, Title = chat.Title ?? text, InviteLink = chat.InviteLink ?? "", IsRequired = true, IsActive = true });
            await _db.SaveChangesAsync(ct);
            await _tg.SendMessageAsync(chatId, $"✅ Kanal qo'shildi: <b>{chat.Title}</b>", KB.AdminMenu(), ct);
        }
        catch { await _tg.SendMessageAsync(chatId, "❌ Kanal topilmadi. Bot kanalga admin sifatida qo'shilganmi?", KB.AdminMenu(), ct); }
    }

    // ═══ HELPERS ═════════════════════════════════════════════════════════
    private async Task<bool> CheckSubscriptionAsync(long tgId, long chatId, Language lang, CancellationToken ct)
    {
        var channels = await _db.Channels.Where(x => x.IsRequired && x.IsActive).ToListAsync(ct);
        if (!channels.Any()) return true;
        var notJoined = new List<(string, string)>();
        foreach (var ch in channels)
        {
            try
            {
                var member = await _bot.GetChatMember(ch.TelegramChannelId, tgId);
                if (member.Status is ChatMemberStatus.Left or ChatMemberStatus.Kicked)
                    notJoined.Add((ch.Title, ch.InviteLink));
            }
            catch { }
        }
        if (!notJoined.Any()) return true;
        await _tg.SendMessageAsync(chatId, _loc.Get("subscription_check", lang), KB.ChannelSubscription(notJoined, lang), ct);
        return false;
    }

    private async Task<InlineKeyboardMarkup> GetTariffKeyboard(CancellationToken ct)
    {
        var tariffs = await _db.Tariffs.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return KB.TariffList(tariffs.Select(t => (t.Id, t.Name, t.Price, t.IsRecommended)));
    }

    // ═══ CALLBACKS ════════════════════════════════════════════════════════
    private async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data is null || cb.From is null) return;
        var chatId  = cb.Message?.Chat.Id ?? cb.From.Id;
        var tgId    = cb.From.Id;
        var isAdmin = _adminIds.Contains(tgId);

        await _tg.AnswerCallbackAsync(cb.Id, ct: ct);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
        var lang = user?.Language ?? Language.UzLatin;

        var parts = cb.Data.Split(':');
        var action = parts[0];
        var sub1   = parts.Length > 1 ? parts[1] : "";
        var sub2   = parts.Length > 2 ? parts[2] : "";
        var sub3   = parts.Length > 3 ? parts[3] : "";

        switch (action)
        {
            case "lang":
                await HandleLanguageCallbackAsync(tgId, chatId, sub1, ct); break;
            case "role":
                await HandleRoleCallbackAsync(tgId, chatId, sub1, user, ct); break;
            case "terms" when sub1 == "accept":
                await HandleTermsAcceptAsync(tgId, chatId, user, ct); break;

            case "tariff" when sub1 == "buy" && long.TryParse(sub2, out var tid):
                await HandleTariffBuyAsync(tgId, chatId, tid, user, ct); break;
            case "tariff" when sub1 == "add" && isAdmin:
                await _state.SetStateAsync(tgId, UserState.WaitingTariffName, null, ct);
                await _tg.SendMessageAsync(chatId, "⭐ Yangi tarif nomi:", KB.CancelMenu(Language.UzLatin), ct); break;

            case "pay" when isAdmin:
                await HandlePayCallbackAsync(sub1, sub2, tgId, chatId, ct); break;

            case "post" when sub1 == "phone" && long.TryParse(sub2, out var postId):
                await HandleShowPhoneCallbackAsync(tgId, chatId, postId, user, ct); break;

            case "match" when sub1 == "more":
                if (user is not null) await ShowMatchingAdsForUserAsync(chatId, user, ct); break;

            case "user" when isAdmin && sub1 == "givetariff_confirm" &&
                long.TryParse(sub2, out var gtUser) && long.TryParse(sub3, out var gtTariff):
                await HandleGiveTariffConfirmAsync(gtUser, gtTariff, chatId, ct); break;
            case "user" when isAdmin:
                await HandleUserCallbackAsync(sub1, sub2, tgId, chatId, ct); break;

            case "broadcast" when sub1 == "send" && isAdmin:
                await HandleBroadcastSendAsync(tgId, chatId, ct); break;

            case "check" when sub1 == "subscription":
                await HandleCheckSubscriptionCallbackAsync(tgId, chatId, lang, ct); break;

            case "cabinet":
                await HandleCabinetCallbackAsync(sub1, chatId, user, ct); break;

            case "settings" when sub1 == "language":
                await _tg.SendMessageAsync(chatId, _loc.Get("choose_language", lang), KB.LanguageMenu(), ct); break;
            case "settings" when isAdmin:
                await HandleSettingsCallbackAsync(sub1, tgId, chatId, ct); break;

            case "admin" when isAdmin:
                await HandleAdminCallbackAsync(sub1, tgId, chatId, ct); break;
            case "card" when isAdmin:
                await HandleCardCallbackAsync(sub1, tgId, chatId, ct); break;
            case "mandatory" when isAdmin:
                await HandleMandatoryCallbackAsync(sub1, tgId, chatId, ct); break;
            case "clear" when isAdmin:
                await HandleClearCallbackAsync(sub1, chatId, ct); break;

            case "account" when sub1 == "add":
                await HandleAccountAddStartAsync(tgId, chatId, user, lang, ct); break;
            case "account" when sub1 == "delete" && long.TryParse(sub2, out var accId):
                await HandleAccountDeleteAsync(tgId, chatId, accId, ct); break;

            case "vnumber":
                await _tg.SendMessageAsync(chatId,
                    "📞 Virtual raqamlar xizmati tez orada ishga tushadi!", ct: ct); break;

            case "menu":
                if (sub1 == "main")
                {
                    var kb = isAdmin ? (ReplyMarkup)KB.AdminMenu() : KB.MainMenu(lang);
                    await _tg.SendMessageAsync(chatId, isAdmin ? "👨‍💼 Admin panel" : _loc.Get("main_menu", lang), kb, ct);
                }
                else if (sub1 == "cancel")
                {
                    await _state.ClearStateAsync(tgId, ct);
                    var kb = isAdmin ? (ReplyMarkup)KB.AdminMenu() : KB.MainMenu(lang);
                    await _tg.SendMessageAsync(chatId, _loc.Get("cancelled", lang), kb, ct);
                }
                break;
        }
    }

    private async Task HandleShowPhoneCallbackAsync(long tgId, long chatId,
        long postId, Domain.Entities.User? user, CancellationToken ct)
    {
        if (user is null) return;
        var hasSub = await HasActiveSub(user.Id, ct);
        if (!hasSub)
        {
            await _tg.AnswerCallbackAsync("", "❌ VIP obuna kerak!", ct);
            await _tg.SendMessageAsync(chatId,
                _loc.Get("no_subscription_for_post", user.Language),
                await GetTariffKeyboard(ct), ct);
            return;
        }
        var post = await _db.Posts.FirstOrDefaultAsync(x => x.Id == postId, ct);
        if (post is null) return;
        post.ContactViews++;
        await _db.SaveChangesAsync(ct);
        await _tg.SendMessageAsync(chatId, $"📞 <code>{post.ContactPhone}</code>", ct: ct);
    }

    private async Task HandleLanguageCallbackAsync(long tgId, long chatId, string langCode, CancellationToken ct)
    {
        var language = langCode switch
        {
            "uz_latin"   => Language.UzLatin,
            "uz_cyrillic" => Language.UzCyrillic,
            "russian"    => Language.Russian,
            "english"    => Language.English,
            "turkish"    => Language.Turkish,
            _            => Language.UzLatin
        };

        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
        if (user is null)
        {
            user = new Domain.Entities.User { TelegramId = tgId, Language = language, FullName = "Foydalanuvchi" };
            await _db.Users.AddAsync(user, ct);
            await _db.SaveChangesAsync(ct);
            await _state.SetStateAsync(tgId, UserState.WaitingRole, null, ct);
            await _tg.SendMessageAsync(chatId, _loc.Get("onboard_step_role", language), KB.RoleMenu(language), ct);
        }
        else if (user.IsOnboarded)
        {
            user.Language = language;
            await _db.SaveChangesAsync(ct);
            await _state.ClearStateAsync(tgId, ct);
            var isAdmin = _adminIds.Contains(tgId);
            var kb = isAdmin ? (ReplyMarkup)KB.AdminMenu() : KB.MainMenu(language);
            await _tg.SendMessageAsync(chatId, _loc.Get("main_menu", language), kb, ct);
        }
        else
        {
            user.Language = language;
            await _db.SaveChangesAsync(ct);
            await _state.SetStateAsync(tgId, UserState.WaitingRole, null, ct);
            await _tg.SendMessageAsync(chatId, _loc.Get("onboard_step_role", language), KB.RoleMenu(language), ct);
        }
    }

    private async Task HandleRoleCallbackAsync(long tgId, long chatId, string roleCode,
        Domain.Entities.User? user, CancellationToken ct)
    {
        if (user is null) return;
        user.Role = roleCode switch { "driver" => UserRole.Driver, "cargo_owner" => UserRole.CargoOwner, _ => UserRole.Logist };
        await _db.SaveChangesAsync(ct);
        var tosKb = new InlineKeyboardMarkup(new[] { new[] { InlineKeyboardButton.WithCallbackData(_loc.Get("terms_accept_btn", user.Language), "terms:accept") } });
        await _state.SetStateAsync(tgId, UserState.WaitingTerms, null, ct);
        await _tg.SendMessageAsync(chatId, _loc.Get("onboard_step_terms", user.Language), tosKb, ct);
    }

    private async Task HandleTermsAcceptAsync(long tgId, long chatId,
        Domain.Entities.User? user, CancellationToken ct)
    {
        if (user is null) return;
        await _state.SetStateAsync(tgId, UserState.WaitingFullName, null, ct);
        await _tg.SendMessageAsync(chatId, _loc.Get("onboard_step_name", user.Language), ct: ct);
    }

    private async Task HandleTariffBuyAsync(long tgId, long chatId, long tariffId,
        Domain.Entities.User? user, CancellationToken ct)
    {
        if (user is null) return;
        var tariff = await _db.Tariffs.FirstOrDefaultAsync(x => x.Id == tariffId, ct);
        if (tariff is null) return;
        var cards = await _db.Cards.Where(x => x.IsActive).ToListAsync(ct);
        if (!cards.Any()) { await _tg.SendMessageAsync(chatId, "❌ Hozircha to'lov kartalari yo'q.\n📞 @TruckBorAdmin", ct: ct); return; }
        var text = $"💳 <b>{tariff.Name} — {tariff.Price:N0} so'm/oy</b>\n\n" +
                   $"📋 <b>Tarif tarkibi:</b>\n" +
                   $"• 👥 {tariff.MaxAccounts} ta Telegram akkaunt\n" +
                   $"• 🌐 {tariff.MaxGroups} ta guruhga e'lon\n" +
                   $"• 📤 Kuniga {tariff.PostsPerDay} e'lon\n" +
                   $"• ⏱ Har {tariff.PostIntervalMinutes} minutda\n\n" +
                   "━━━━━━━━━━━━━━━━━━━━━━\n" +
                   "💳 Quyidagi kartaga to'lov qiling:\n\n";
        foreach (var c in cards)
        {
            text += $"💳 <code>{c.CardNumber}</code>\n👤 {c.CardHolder}";
            if (!string.IsNullOrEmpty(c.BankName)) text += $"\n🏦 {c.BankName}";
            text += "\n\n";
        }
        text += "📸 To'lov chekini yuboring.";
        await _state.SetStateAsync(tgId, UserState.WaitingPaymentCheck, tariffId.ToString(), ct);
        await _tg.SendMessageAsync(chatId, text, KB.CancelMenu(user.Language), ct);
    }

    private async Task HandlePayCallbackAsync(string action, string pidStr, long adminId, long chatId, CancellationToken ct)
    {
        if (!long.TryParse(pidStr, out var paymentId)) return;
        if (action == "approve")
        {
            await _state.SetStateAsync(adminId, UserState.WaitingPaymentAmount, paymentId.ToString(), ct);
            await _tg.SendMessageAsync(chatId, "💵 To'lov summasini kiriting:\n<code>99000</code>", ct: ct);
        }
        else if (action == "reject")
        {
            await _state.SetStateAsync(adminId, UserState.WaitingRejectReason, paymentId.ToString(), ct);
            await _tg.SendMessageAsync(chatId, "❌ Rad etish sababini kiriting:\n(<code>-</code> sabab yo'q bo'lsa)", ct: ct);
        }
        else if (action == "viewuser")
        {
            var p = await _db.Payments.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == paymentId, ct);
            if (p?.User is null) return;
            await _tg.SendMessageAsync(chatId,
                $"👤 <b>{p.User.FullName}</b>\n📱 {p.User.PhoneNumber}\n💰 {p.User.Balance:N0} so'm\n🆔 <code>{p.User.TelegramId}</code>",
                KB.UserActions(p.User.TelegramId, p.User.IsBlocked), ct);
        }
    }

    private async Task HandleUserCallbackAsync(string action, string uidStr, long adminId, long chatId, CancellationToken ct)
    {
        if (!long.TryParse(uidStr, out var userTgId)) return;
        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == userTgId, ct);
        if (user is null) return;
        switch (action)
        {
            case "ban":
                user.IsBlocked = true; await _db.SaveChangesAsync(ct);
                try { await _tg.SendMessageAsync(userTgId, "🚫 Hisobingiz bloklandi.", ct: ct); } catch { }
                await _tg.SendMessageAsync(chatId, $"🚫 {user.FullName} bloklandi.", KB.AdminMenu(), ct); break;
            case "unban":
                user.IsBlocked = false; await _db.SaveChangesAsync(ct);
                try { await _tg.SendMessageAsync(userTgId, "✅ Hisobingiz faollashtirildi.", ct: ct); } catch { }
                await _tg.SendMessageAsync(chatId, $"✅ {user.FullName} blokdan chiqarildi.", KB.AdminMenu(), ct); break;
            case "balance":
                await _state.SetStateAsync(adminId, UserState.WaitingBalanceAmount, user.Id.ToString(), ct);
                await _tg.SendMessageAsync(chatId, $"👤 {user.FullName}\n💰 Joriy: {user.Balance:N0} so'm\n\nMiqdor kiriting (+50000 yoki -10000):", ct: ct); break;
            case "msg":
                await _state.SetStateAsync(adminId, UserState.WaitingUserMessage, user.TelegramId.ToString(), ct);
                await _tg.SendMessageAsync(chatId, $"✉️ {user.FullName} ga xabar yozing:", ct: ct); break;
            case "delete":
                await _tg.SendMessageAsync(chatId, $"⚠️ {user.FullName} ni o'chirasizmi?", KB.Confirm($"user:delete_confirm:{userTgId}", "🗑 Ha"), ct); break;
            case "delete_confirm":
                _db.Users.Remove(user); await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, $"🗑 {user.FullName} o'chirildi.", KB.AdminMenu(), ct); break;
            case "givetariff":
                var tariffs = await _db.Tariffs.Where(x => x.IsActive).ToListAsync(ct);
                await _tg.SendMessageAsync(chatId, $"⭐ {user.FullName} ga tarif bering:", KB.GiveTariffList(userTgId, tariffs.Select(t => (t.Id, t.Name, t.DurationDays))), ct); break;
            case "givepremium":
                user.IsPremium = true; await _db.SaveChangesAsync(ct);
                try { await _tg.SendMessageAsync(userTgId, "💎 Premium aktivlashtirildi!", ct: ct); } catch { }
                await _tg.SendMessageAsync(chatId, $"💎 {user.FullName} ga premium berildi.", KB.AdminMenu(), ct); break;
        }
    }

    private async Task HandleBroadcastSendAsync(long adminId, long chatId, CancellationToken ct)
    {
        var text = await _state.GetStateDataAsync<string>(adminId, ct);
        await _state.ClearStateAsync(adminId, ct);
        if (string.IsNullOrEmpty(text)) return;
        var users  = await _db.Users.Where(x => !x.IsBlocked).ToListAsync(ct);
        var sent   = 0; var failed = 0;
        foreach (var u in users)
        {
            try { await _tg.SendMessageAsync(u.TelegramId, text, ct: ct); sent++; await Task.Delay(50, ct); }
            catch { failed++; }
        }
        await _tg.SendMessageAsync(chatId,
            $"✅ <b>Broadcast tugadi!</b>\n\n📨 Yuborildi: <b>{sent}</b>\n❌ Yuborilmadi: <b>{failed}</b>",
            KB.AdminMenu(), ct);
    }

    private async Task HandleCheckSubscriptionCallbackAsync(long tgId, long chatId, Language lang, CancellationToken ct)
    {
        if (await CheckSubscriptionAsync(tgId, chatId, lang, ct))
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
            var ul   = user?.Language ?? lang;
            await _tg.SendMessageAsync(chatId, _loc.Get("subscription_ok", ul), KB.MainMenu(ul), ct);
        }
    }

    private async Task HandleCabinetCallbackAsync(string action, long chatId,
        Domain.Entities.User? user, CancellationToken ct)
    {
        if (user is null) return;
        switch (action)
        {
            case "balance": await ShowCabinetAsync(chatId, user, ct); break;
            case "payments":
                var payments = await _db.Payments.Where(x => x.UserId == user.Id)
                    .OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
                var pText = "📋 <b>To'lovlar tarixi</b>\n\n";
                foreach (var p in payments)
                {
                    var st = p.Status switch { PaymentStatus.Approved => "✅", PaymentStatus.Rejected => "❌", _ => "⏳" };
                    pText += $"{st} {p.Amount:N0} so'm — {p.CreatedAt:dd.MM.yyyy HH:mm}\n";
                }
                if (!payments.Any()) pText += "To'lovlar yo'q.";
                await _tg.SendMessageAsync(chatId, pText, ct: ct); break;
            case "posts":
                var hasSub = await HasActiveSub(user.Id, ct);
                var posts  = await _db.Posts.Where(x => x.UserId == user.Id)
                    .OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(ct);
                var ptText = "📦 <b>Mening e'lonlarim</b>\n\n";
                foreach (var p in posts)
                {
                    var st = p.Status == PostStatus.Active ? "✅" : "❌";
                    var typeIcon = p.PostType switch { PostType.Transport => "🚛", PostType.Dogruz => "📮", _ => "📦" };
                    ptText += $"{st} {typeIcon} {p.FromCity} → {p.ToCity}\n";
                    ptText += $"   👁 {p.ViewCount} ko'rish | 📞 {p.ContactViews} murojaat\n";
                    ptText += $"   📅 {p.CreatedAt:dd.MM.yyyy}\n\n";
                }
                if (!posts.Any()) ptText += "E'lonlar yo'q.";
                await _tg.SendMessageAsync(chatId, ptText, ct: ct); break;
            case "tariff":
                var sub = await _db.Subscriptions.Include(x => x.Tariff)
                    .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                        x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);
                var tariffText = sub is not null
                    ? $"⭐ <b>Faol tarif</b>\n\n📋 {sub.Tariff?.Name}\n📅 {sub.StartDate:dd.MM.yyyy} — {sub.EndDate:dd.MM.yyyy}\n⏰ Qoldi: <b>{sub.DaysLeft}</b> kun\n💰 Balans: {user.Balance:N0} so'm"
                    : $"⭐ <b>Tarif yo'q</b>\n\n💰 Balans: {user.Balance:N0} so'm\n\nE'lon berish uchun VIP tarif oling!";
                await _tg.SendMessageAsync(chatId, tariffText, ct: ct); break;
            case "miniapp":
                await _tg.SendMessageAsync(chatId,
                    "🌐 <b>TruckBor Mini App</b>\n\n📱 Ilovani oching:\nhttps://t.me/TruckBorBot/app\n\nYoki: app.truckbor.uz", ct: ct); break;
        }
    }

    private async Task HandleSettingsCallbackAsync(string key, long adminId, long chatId, CancellationToken ct)
    {
        await _state.SetStateAsync(adminId, UserState.WaitingSettingsValue, key, ct);
        var current = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
        await _tg.SendMessageAsync(chatId,
            $"⚙️ <b>{key}</b>\n\nJoriy: {current?.Value ?? "Yo'q"}\n\nYangi qiymat kiriting:",
            KB.CancelMenu(Language.UzLatin), ct);
    }

    private async Task HandleAdminCallbackAsync(string action, long adminId, long chatId, CancellationToken ct)
    {
        switch (action)
        {
            case "search_user":
                await _state.SetStateAsync(adminId, UserState.WaitingUserSearch, null, ct);
                await _tg.SendMessageAsync(chatId, "🔍 Ism, telefon yoki TG ID kiriting:", KB.CancelMenu(Language.UzLatin), ct); break;
            case "add":
                await _state.SetStateAsync(adminId, UserState.WaitingUserSearch, "add_admin", ct);
                await _tg.SendMessageAsync(chatId, "👑 Yangi adminning Telegram ID sini kiriting:", ct: ct); break;
        }
    }

    private async Task HandleCardCallbackAsync(string action, long adminId, long chatId, CancellationToken ct)
    {
        if (action == "add")
        {
            await _state.SetStateAsync(adminId, UserState.WaitingCardNumber, null, ct);
            await _tg.SendMessageAsync(chatId, "💳 Karta raqamini kiriting:\n<code>8600 1234 5678 9012</code>", KB.CancelMenu(Language.UzLatin), ct);
        }
    }

    private async Task HandleMandatoryCallbackAsync(string action, long adminId, long chatId, CancellationToken ct)
    {
        switch (action)
        {
            case "add":
                await _state.SetStateAsync(adminId, UserState.WaitingChannelId, null, ct);
                await _tg.SendMessageAsync(chatId, "📢 Kanal ID sini kiriting:\n<code>-1001234567890</code>", KB.CancelMenu(Language.UzLatin), ct); break;
            case "on":
            case "off":
                var chs = await _db.Channels.Where(x => x.IsRequired).ToListAsync(ct);
                foreach (var c in chs) c.IsActive = action == "on";
                await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, action == "on" ? "🟢 Majburiy obuna yoqildi!" : "🔴 O'chirildi!", KB.AdminMenu(), ct); break;
            case "clear":
                var allCh = await _db.Channels.ToListAsync(ct);
                _db.Channels.RemoveRange(allCh);
                await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, "🗑 Barcha kanallar o'chirildi.", KB.AdminMenu(), ct); break;
        }
    }

    private async Task HandleClearCallbackAsync(string target, long chatId, CancellationToken ct)
    {
        switch (target)
        {
            case "users":     await _tg.SendMessageAsync(chatId, "⚠️ Foydalanuvchilarni o'chirasizmi?", KB.Confirm("clear:users_confirm", "🗑 Ha"), ct); break;
            case "payments":  await _tg.SendMessageAsync(chatId, "⚠️ To'lovlarni o'chirasizmi?",         KB.Confirm("clear:payments_confirm", "🗑 Ha"), ct); break;
            case "subs":      await _tg.SendMessageAsync(chatId, "⚠️ Obunalarni o'chirasizmi?",          KB.Confirm("clear:subs_confirm", "🗑 Ha"), ct); break;
            case "posts":     await _tg.SendMessageAsync(chatId, "⚠️ E'lonlarni o'chirasizmi?",          KB.Confirm("clear:posts_confirm", "🗑 Ha"), ct); break;
            case "all":       await _tg.SendMessageAsync(chatId, "⚠️ <b>HAMMASINI</b> o'chirasizmi?",    KB.Confirm("clear:all_confirm", "⚠️ Ha"), ct); break;
            case "users_confirm":    _db.Users.RemoveRange(await _db.Users.ToListAsync(ct)); await _db.SaveChangesAsync(ct); await _tg.SendMessageAsync(chatId, "✅ Foydalanuvchilar o'chirildi.", KB.AdminMenu(), ct); break;
            case "payments_confirm": _db.Payments.RemoveRange(await _db.Payments.ToListAsync(ct)); await _db.SaveChangesAsync(ct); await _tg.SendMessageAsync(chatId, "✅ To'lovlar o'chirildi.", KB.AdminMenu(), ct); break;
            case "subs_confirm":     _db.Subscriptions.RemoveRange(await _db.Subscriptions.ToListAsync(ct)); await _db.SaveChangesAsync(ct); await _tg.SendMessageAsync(chatId, "✅ Obunalar o'chirildi.", KB.AdminMenu(), ct); break;
            case "posts_confirm":    _db.Posts.RemoveRange(await _db.Posts.ToListAsync(ct)); await _db.SaveChangesAsync(ct); await _tg.SendMessageAsync(chatId, "✅ E'lonlar o'chirildi.", KB.AdminMenu(), ct); break;
            case "all_confirm":
                _db.Payments.RemoveRange(await _db.Payments.ToListAsync(ct));
                _db.Subscriptions.RemoveRange(await _db.Subscriptions.ToListAsync(ct));
                _db.Posts.RemoveRange(await _db.Posts.ToListAsync(ct));
                _db.Users.RemoveRange(await _db.Users.ToListAsync(ct));
                await _db.SaveChangesAsync(ct);
                await _tg.SendMessageAsync(chatId, "✅ Hammasi o'chirildi.", KB.AdminMenu(), ct); break;
        }
    }

    private async Task HandleGiveTariffConfirmAsync(long userTgId, long tariffId, long chatId, CancellationToken ct)
    {
        var user   = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == userTgId, ct);
        var tariff = await _db.Tariffs.FirstOrDefaultAsync(x => x.Id == tariffId, ct);
        if (user is null || tariff is null) return;
        var exSub = await _db.Subscriptions.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);
        if (exSub is not null) exSub.EndDate = exSub.EndDate.AddDays(tariff.DurationDays);
        else _db.Subscriptions.Add(new Domain.Entities.Subscription { UserId = user.Id, TariffId = tariff.Id, Status = SubscriptionStatus.Active, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(tariff.DurationDays) });
        await _db.SaveChangesAsync(ct);
        try { await _tg.SendMessageAsync(userTgId, $"🎁 Sizga <b>{tariff.Name}</b> tarifi berildi!\n📅 {tariff.DurationDays} kun", ct: ct); } catch { }
        await _tg.SendMessageAsync(chatId, $"✅ {user.FullName} ga <b>{tariff.Name}</b> tarifi berildi.", KB.AdminMenu(), ct);
    }

    // ═══ ACCOUNT MANAGEMENT ═══════════════════════════════════════════════
    private async Task HandleAccountAddStartAsync(long tgId, long chatId,
        Domain.Entities.User? user, Language lang, CancellationToken ct)
    {
        if (user is null) return;

        var sub = await _db.Subscriptions
            .Include(x => x.Tariff)
            .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                x.Status == SubscriptionStatus.Active && x.EndDate > DateTime.UtcNow, ct);

        if (sub?.Tariff is null)
        {
            await _tg.SendMessageAsync(chatId,
                "❌ Akkaunt qo'shish uchun VIP obuna kerak!", await GetTariffKeyboard(ct), ct);
            return;
        }

        var existing = await _db.TelegramAccounts.CountAsync(x => x.UserId == user.Id, ct);
        if (existing >= sub.Tariff.MaxAccounts)
        {
            await _tg.SendMessageAsync(chatId,
                $"❌ Maksimal akkaunt soni: <b>{sub.Tariff.MaxAccounts}</b>\nTarifingizni yangilang.", ct: ct);
            return;
        }

        await _state.SetStateAsync(tgId, UserState.WaitingAccountPhone, null, ct);
        await _tg.SendMessageAsync(chatId,
            "📱 <b>Akkaunt qo'shish</b>\n\nTelegram akkauntingizning telefon raqamini yuboring:",
            KB.RequestContact(lang), ct);
    }

    private async Task HandleAccountPhoneAsync(long tgId, long chatId,
        Message msg, Domain.Entities.User user, CancellationToken ct)
    {
        var phone = msg.Contact?.PhoneNumber ?? msg.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(phone))
        {
            await _tg.SendMessageAsync(chatId,
                "❌ Telefon raqamini yuboring yoki kiriting.", ct: ct);
            return;
        }
        if (!phone.StartsWith("+")) phone = "+" + phone;

        await _state.ClearStateAsync(tgId, ct);

        if (await _db.TelegramAccounts.AnyAsync(x => x.UserId == user.Id && x.PhoneNumber == phone, ct))
        {
            await _tg.SendMessageAsync(chatId,
                "⚠️ Bu raqam allaqachon qo'shilgan.", KB.MainMenu(user.Language), ct);
            return;
        }

        _db.TelegramAccounts.Add(new Domain.Entities.TelegramAccount
        {
            UserId      = user.Id,
            PhoneNumber = phone,
            IsActive    = true,
        });
        await _db.SaveChangesAsync(ct);

        await _tg.SendMessageAsync(chatId,
            $"✅ <b>Akkaunt qo'shildi!</b>\n\n📞 {phone}\n\n" +
            "💡 Bu raqam guruh postlash uchun ishlatiladi.",
            KB.MainMenu(user.Language), ct);
    }

    private async Task HandleAccountDeleteAsync(long tgId, long chatId,
        long accountId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.TelegramId == tgId, ct);
        if (user is null) return;

        var account = await _db.TelegramAccounts
            .FirstOrDefaultAsync(x => x.Id == accountId && x.UserId == user.Id, ct);
        if (account is null) return;

        _db.TelegramAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);

        await _tg.SendMessageAsync(chatId,
            $"🗑 Akkaunt o'chirildi: {account.PhoneNumber}", ct: ct);

        await ShowAccountsAsync(chatId, user, ct);
    }

    // ═══ POST DRAFT ═══════════════════════════════════════════════════════
    private sealed class PostDraft
    {
        public PostType Type        { get; set; } = PostType.Cargo;
        public string   From        { get; set; } = "";
        public string   To          { get; set; } = "";
        public string   CargoType   { get; set; } = "";
        public string   Weight      { get; set; } = "";
        public string   VehicleType { get; set; } = "";
        public string   Price       { get; set; } = "";
    }
}
