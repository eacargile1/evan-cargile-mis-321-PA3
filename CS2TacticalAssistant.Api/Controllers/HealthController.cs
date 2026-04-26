using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

/// <summary>Lightweight health check for load balancers and "did my deploy work?" tests.</summary>
[ApiController]
[Route("api")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Get() =>
        Ok(new
        {
            status = "ok",
            timeUtc = DateTime.UtcNow,
        });
}
