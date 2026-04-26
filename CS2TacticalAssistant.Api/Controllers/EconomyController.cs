using CS2TacticalAssistant.Api.Models;
using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/economy")]
public sealed class EconomyController(IEconomyService economy) : ControllerBase
{
    [HttpPost("explain")]
    public ActionResult<EconomyExplanationDto> Explain([FromBody] EconomyExplainRequest req) =>
        Ok(economy.Explain(req));
}
