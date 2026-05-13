using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TruckBor.Infrastructure.Data;

namespace TruckBor.API.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController(
    AppDbContext db,
    IConfiguration config,
    ILogger<AdminAuthController> logger) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, AdminDto Admin);
    public record AdminDto(long Id, string FullName, string Username, bool IsSuper, AdminPermsDto Perms);
    public record AdminPermsDto(
        bool CanManageUsers, bool CanManagePayments, bool CanManageTariffs,
        bool CanManageGroups, bool CanManageCards, bool CanManageChannels,
        bool CanBroadcast, bool CanViewStatistics, bool CanManageAdmins,
        bool CanManageSettings, bool CanManageVirtual, bool CanManagePremium,
        bool CanManageVideos);

    // ── Login ─────────────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Username va parol kerak" });

        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == req.Username, ct);

        if (admin is null || admin.PasswordHash is null)
            return Unauthorized(new { error = "Username yoki parol noto'g'ri" });

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash); }
        catch { valid = false; }

        if (!valid)
        {
            logger.LogWarning("Failed admin login attempt for username: {Username}", req.Username);
            return Unauthorized(new { error = "Username yoki parol noto'g'ri" });
        }

        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var token = IssueJwt(admin);
        logger.LogInformation("Admin logged in: {Username} (Id={Id})", admin.Username, admin.Id);

        return Ok(new LoginResponse(token, new AdminDto(
            admin.Id, admin.FullName, admin.Username!,
            admin.IsSuper,
            new AdminPermsDto(
                admin.CanManageUsers, admin.CanManagePayments, admin.CanManageTariffs,
                admin.CanManageGroups, admin.CanManageCards, admin.CanManageChannels,
                admin.CanBroadcast, admin.CanViewStatistics, admin.CanManageAdmins,
                admin.CanManageSettings, admin.CanManageVirtual, admin.CanManagePremium,
                admin.CanManageVideos))));
    }

    // ── Refresh / Whoami ──────────────────────────────────────────────────
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var adminId)) return Unauthorized();

        var admin = await db.AdminUsers.FindAsync([adminId], ct);
        if (admin is null) return Unauthorized();

        return Ok(new AdminDto(
            admin.Id, admin.FullName, admin.Username ?? "",
            admin.IsSuper,
            new AdminPermsDto(
                admin.CanManageUsers, admin.CanManagePayments, admin.CanManageTariffs,
                admin.CanManageGroups, admin.CanManageCards, admin.CanManageChannels,
                admin.CanBroadcast, admin.CanViewStatistics, admin.CanManageAdmins,
                admin.CanManageSettings, admin.CanManageVirtual, admin.CanManagePremium,
                admin.CanManageVideos)));
    }

    // ── Change password ───────────────────────────────────────────────────
    [HttpPost("change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idClaim, out var adminId)) return Unauthorized();

        var admin = await db.AdminUsers.FindAsync([adminId], ct);
        if (admin is null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, admin.PasswordHash!))
            return BadRequest(new { error = "Eski parol noto'g'ri" });

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Parol muvaffaqiyatli o'zgartirildi" });
    }

    public record ChangePasswordRequest(string OldPassword, string NewPassword);

    // ── Helpers ───────────────────────────────────────────────────────────
    private string IssueJwt(Domain.Entities.AdminUser admin)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: [
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.FullName),
                new Claim("username", admin.Username ?? ""),
                new Claim("role", "admin"),
                new Claim("is_super", admin.IsSuper.ToString()),
            ],
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
