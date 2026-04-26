using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/lineups")]
public sealed class LineupsController(ILineupService lineups) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string map,
        [FromQuery] string? site,
        [FromQuery] string? grenade_type,
        [FromQuery] string? side,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(map)) return BadRequest(new { error = "map is required." });
        return Ok(await lineups.SearchAsync(map, site, grenade_type, side, ct));
    }
}
