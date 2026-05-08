using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using TruckBor.Infrastructure.Telegram.Handlers;

namespace TruckBor.API.Controllers.Public;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly BotUpdateHandler _handler;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(BotUpdateHandler handler, ILogger<WebhookController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken ct)
    {
        if (update is null) return Ok();
        await _handler.HandleUpdateAsync(update, ct);
        return Ok();
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", time = DateTime.UtcNow });
}