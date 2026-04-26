using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(IMatchService matches) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] string? tournament, CancellationToken ct) =>
        Ok(await matches.GetUpcomingAsync(tournament, onlyMatchTodayUtc: true, ct));
}
