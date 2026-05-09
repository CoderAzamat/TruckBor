using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TruckBor.Domain.Common;
using TruckBor.Domain.Entities;
using TruckBor.Domain.Enums;
using TruckBor.Infrastructure.Data;

namespace TruckBor.API.Controllers.MiniApp;

[ApiController]
[Route("api/miniapp")]
public class MiniAppController(AppDbContext db, IConfiguration config) : ControllerBase
{
    // ── DTOs ─────────────────────────────────────────────────────────────────

    public record AuthRequest(string InitData);

    public record AuthResponse(string Token, UserDto User);

    public record UserDto(
        long Id, long TelegramId, string? Username,
        string FirstName, string? LastName,
        string? PhoneNumber, decimal Balance,
        string Role, bool IsVip, bool IsOnboarded, int TotalPosts);

    public record PostDto(
        long Id, long UserId, string UserFullName, string UserRole,
        string PostType,
        string FromCity, string ToCity,
        double? FromLat, double? FromLng, double? ToLat, double? ToLng,
        string? CargoType, string? Weight, string? VehicleType,
        string? Price, string? Description,
        bool IsVerified, int ViewCount,
        string CreatedAt, string ExpiresAt);

    public record PaginatedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize, bool HasMore);

    public record CreatePostRequest(
        string PostType,
        string FromCity,
        string ToCity,
        string? CargoType,
        string? Weight,
        string? VehicleType,
        string? Price,
        string ContactPhone,
        string? Description);

    public record TariffDto(
        long Id, string Name, string? Description,
        decimal Price, int DurationDays, bool IsActive, bool IsRecommended,
        int MaxGroups, int PostsPerDay, int MaxAccounts,
        string[] Features);

    public record CardDto(long Id, string CardNumber, string CardHolder, string BankName, bool IsActive);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private UserDto ToDto(User u)
    {
        var parts = u.FullName.Split(' ', 2);
        return new UserDto(
            u.Id, u.TelegramId, u.Username,
            parts[0], parts.Length > 1 ? parts[1] : null,
            u.PhoneNumber, u.Balance,
            u.Role.ToString(), u.IsPremium, u.IsOnboarded, u.TotalPosts);
    }

    private static PostDto ToDto(Post p) => new(
        p.Id, p.UserId, p.User?.FullName ?? "",
        p.PostedBy.ToString(),
        p.PostType.ToString(),
        p.FromCity, p.ToCity,
        p.FromLat, p.FromLng, p.ToLat, p.ToLng,
        p.CargoType, p.Weight, p.VehicleType,
        p.Price, p.Description,
        p.IsVerified, p.ViewCount,
        p.CreatedAt.ToString("O"), p.ExpiresAt.ToString("O"));

    private long GetUserId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string IssueJwt(User u)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: [
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Name, u.FullName),
                new Claim("telegram_id", u.TelegramId.ToString()),
            ],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [HttpPost("auth")]
    public async Task<IActionResult> Auth([FromBody] AuthRequest req, CancellationToken ct)
    {
        var telegramId = ValidateInitData(req.InitData, config["Bot:Token"]!);
        if (telegramId == null)
            return Unauthorized(new { error = "Invalid initData" });

        // Parse user data from initData
        var query = System.Web.HttpUtility.ParseQueryString(req.InitData);
        string? firstName = null, lastName = null, username = null;
        if (query["user"] is { } userJson)
        {
            using var doc = JsonDocument.Parse(userJson);
            var root = doc.RootElement;
            firstName = root.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
            lastName  = root.TryGetProperty("last_name",  out var ln) ? ln.GetString() : null;
            username  = root.TryGetProperty("username",   out var un) ? un.GetString() : null;
        }

        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(s => !string.IsNullOrEmpty(s)));
        if (string.IsNullOrEmpty(fullName)) fullName = $"User {telegramId}";

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId, ct);
        if (user == null)
        {
            user = new User
            {
                TelegramId = telegramId.Value,
                FullName   = fullName,
                Username   = username,
                IsOnboarded = false,
                Language   = Language.UzLatin,
            };
            db.Users.Add(user);
        }
        else
        {
            user.FullName = fullName;
            if (username != null) user.Username = username;
        }

        await db.SaveChangesAsync(ct);

        var token = IssueJwt(user);
        return Ok(new AuthResponse(token, ToDto(user)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = GetUserId();
        var user = await db.Users.FindAsync([uid], ct);
        return user == null ? NotFound() : Ok(ToDto(user));
    }

    // ── Posts ─────────────────────────────────────────────────────────────────

    [HttpGet("posts")]
    public async Task<IActionResult> Posts(
        [FromQuery] string? postType,
        [FromQuery] string? fromCity,
        [FromQuery] string? toCity,
        [FromQuery] string? vehicleType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var q = db.Posts
            .Include(p => p.User)
            .Where(p => p.Status == PostStatus.Active && p.ExpiresAt > DateTime.UtcNow)
            .AsQueryable();

        if (!string.IsNullOrEmpty(postType) && Enum.TryParse<PostType>(postType, out var pt))
            q = q.Where(p => p.PostType == pt);

        if (!string.IsNullOrEmpty(fromCity))
            q = q.Where(p => p.FromCity.ToLower().Contains(fromCity.ToLower()));

        if (!string.IsNullOrEmpty(toCity))
            q = q.Where(p => p.ToCity.ToLower().Contains(toCity.ToLower()));

        if (!string.IsNullOrEmpty(vehicleType))
            q = q.Where(p => p.VehicleType != null && p.VehicleType.ToLower().Contains(vehicleType.ToLower()));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PaginatedResult<PostDto>(
            items.Select(ToDto), total, page, pageSize, (page * pageSize) < total));
    }

    [HttpGet("posts/mine")]
    [Authorize]
    public async Task<IActionResult> MyPosts(CancellationToken ct)
    {
        var uid = GetUserId();
        var posts = await db.Posts
            .Include(p => p.User)
            .Where(p => p.UserId == uid)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return Ok(posts.Select(ToDto));
    }

    [HttpPost("posts")]
    [Authorize]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest req, CancellationToken ct)
    {
        var uid  = GetUserId();
        var user = await db.Users.FindAsync([uid], ct);
        if (user == null) return Unauthorized();

        if (!Enum.TryParse<PostType>(req.PostType, out var pt))
            return BadRequest(new { error = "Invalid postType" });

        var post = new Post
        {
            UserId       = uid,
            PostType     = pt,
            PostedBy     = user.Role,
            FromCity     = req.FromCity.Trim(),
            ToCity       = req.ToCity.Trim(),
            CargoType    = req.CargoType?.Trim(),
            Weight       = req.Weight?.Trim(),
            VehicleType  = req.VehicleType?.Trim(),
            Price        = req.Price?.Trim(),
            Description  = req.Description?.Trim(),
            ContactPhone = req.ContactPhone.Trim(),
            Status       = PostStatus.Active,
            ExpiresAt    = DateTime.UtcNow.AddDays(7),
        };

        db.Posts.Add(post);
        user.TotalPosts++;
        await db.SaveChangesAsync(ct);

        await db.Entry(post).Reference(p => p.User).LoadAsync(ct);
        return Ok(ToDto(post));
    }

    [HttpDelete("posts/{id:long}")]
    [Authorize]
    public async Task<IActionResult> DeletePost(long id, CancellationToken ct)
    {
        var uid  = GetUserId();
        var post = await db.Posts.FindAsync([id], ct);
        if (post == null) return NotFound();
        if (post.UserId != uid) return Forbid();

        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("posts/{id:long}/phone")]
    [Authorize]
    public async Task<IActionResult> ShowPhone(long id, CancellationToken ct)
    {
        var post = await db.Posts.FindAsync([id], ct);
        if (post == null) return NotFound();

        // Increment contact views
        post.ContactViews++;
        await db.SaveChangesAsync(ct);

        return Ok(new { phone = post.ContactPhone ?? "" });
    }

    [HttpPost("posts/{id:long}/view")]
    public async Task<IActionResult> IncrementView(long id, CancellationToken ct)
    {
        var post = await db.Posts.FindAsync([id], ct);
        if (post == null) return NoContent();

        post.ViewCount++;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Map ───────────────────────────────────────────────────────────────────

    [HttpGet("map/posts")]
    public async Task<IActionResult> MapPosts(
        [FromQuery] double minLat, [FromQuery] double maxLat,
        [FromQuery] double minLng, [FromQuery] double maxLng,
        [FromQuery] string? postType,
        CancellationToken ct = default)
    {
        var q = db.Posts
            .Include(p => p.User)
            .Where(p =>
                p.Status == PostStatus.Active &&
                p.ExpiresAt > DateTime.UtcNow &&
                p.FromLat.HasValue && p.FromLng.HasValue &&
                p.FromLat >= minLat && p.FromLat <= maxLat &&
                p.FromLng >= minLng && p.FromLng <= maxLng)
            .AsQueryable();

        if (!string.IsNullOrEmpty(postType) && Enum.TryParse<PostType>(postType, out var pt))
            q = q.Where(p => p.PostType == pt);

        var posts = await q.Take(200).ToListAsync(ct);
        return Ok(posts.Select(ToDto));
    }

    // ── Market ────────────────────────────────────────────────────────────────

    [HttpGet("tariffs")]
    public async Task<IActionResult> Tariffs(CancellationToken ct)
    {
        var tariffs = await db.Tariffs
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

        return Ok(tariffs.Select(t => new TariffDto(
            t.Id, t.Name, t.Description,
            t.DiscountPrice ?? t.Price,
            t.DurationDays, t.IsActive, t.IsRecommended,
            t.MaxGroups, t.PostsPerDay, t.MaxAccounts,
            BuildFeatures(t))));
    }

    [HttpGet("cards")]
    public async Task<IActionResult> Cards(CancellationToken ct)
    {
        var cards = await db.Cards
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return Ok(cards.Select(c => new CardDto(
            c.Id, c.CardNumber, c.CardHolder, c.BankName ?? "", c.IsActive)));
    }

    // ── HMAC Validation ───────────────────────────────────────────────────────

    private static long? ValidateInitData(string initData, string botToken)
    {
        try
        {
            var pairs = System.Web.HttpUtility.ParseQueryString(initData);
            var hash  = pairs["hash"];
            if (string.IsNullOrEmpty(hash)) return null;

            // Build data check string (all except hash, sorted alphabetically)
            var lines = new List<string>();
            foreach (string? key in pairs.AllKeys)
            {
                if (key == null || key == "hash") continue;
                lines.Add($"{key}={pairs[key]}");
            }
            lines.Sort(StringComparer.Ordinal);
            var dataCheckString = string.Join("\n", lines);

            // secret_key = HMAC-SHA256("WebAppData", bot_token)
            using var hmac1 = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
            var secretKey = hmac1.ComputeHash(Encoding.UTF8.GetBytes(botToken));

            // computed_hash = HMAC-SHA256(secret_key, data_check_string)
            using var hmac2 = new HMACSHA256(secretKey);
            var computed = hmac2.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var computedHex = Convert.ToHexString(computed).ToLower();

            if (!computedHex.Equals(hash, StringComparison.OrdinalIgnoreCase))
                return null;

            // Extract user id from user JSON
            var userJson = pairs["user"];
            if (string.IsNullOrEmpty(userJson)) return null;

            using var doc = JsonDocument.Parse(userJson);
            return doc.RootElement.GetProperty("id").GetInt64();
        }
        catch
        {
            return null;
        }
    }

    private static string[] BuildFeatures(Tariff t)
    {
        var features = new List<string>
        {
            $"{t.PostsPerDay} ta e'lon/kun",
            $"{t.MaxGroups} ta guruh",
            $"{t.DurationDays} kun",
        };
        if (t.MaxAccounts > 1) features.Add($"{t.MaxAccounts} akkaunt");
        return features.ToArray();
    }
}
