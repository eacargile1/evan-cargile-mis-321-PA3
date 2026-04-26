using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/updates")]
public sealed class GameUpdatesController(SteamGameNewsService news) : ControllerBase
{
    [HttpGet("valve-cs2")]
    public async Task<IActionResult> ValveCs2([FromQuery] int count = 6, CancellationToken ct = default) =>
        Ok(await news.GetRecentCs2NewsAsync(count, excerptMax: 300, bodyMax: 20_000, ct: ct));
}
