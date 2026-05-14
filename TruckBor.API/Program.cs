using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TruckBor.API.Extensions;
using TruckBor.API.Middleware;
using TruckBor.Application;
using TruckBor.Infrastructure;
using TruckBor.Infrastructure.Data;
using TruckBor.Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/truckbor-.log", rollingInterval: RollingInterval.Day));

// ── Clean Architecture Layers ─────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Controllers & JSON ────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        opts.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });

// ── JWT Auth ──────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // Don't remap "role" → ClaimTypes.Role — keep claim names as-is from JWT
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
});

// ── CORS ──────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("default", b => b
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ── Rate Limiting ─────────────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = 429;
    opt.AddFixedWindowLimiter("webhook", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromSeconds(10);
    });
});

// ── Swagger ───────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Health Checks (simple built-in — v9 packages don't support .NET 10) ───────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Migrate + Seed ────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbSeeder.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Seeding failed");
    }
}

// ── Middleware ────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Static files (Mini App) ───────────────────────────────
app.UseDefaultFiles(new DefaultFilesOptions { RequestPath = "/miniapp" });
app.UseStaticFiles();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// ── Webhook Setup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await scope.ServiceProvider.SetupWebhookAsync(app.Configuration, logger);
}

Log.Information("🚀 TruckBor API starting...");
app.Run();