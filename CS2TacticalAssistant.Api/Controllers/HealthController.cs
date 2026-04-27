using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CS2TacticalAssistant.Api.Controllers;

/// <summary>Lightweight health check for load balancers and "did my deploy work?" tests.</summary>
[ApiController]
[Route("api")]
public sealed class HealthController(IDatabaseService db) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Get() =>
        Ok(new
        {
            status = "ok",
            timeUtc = DateTime.UtcNow,
        });

    /// <summary>Whether OpenAI + MySQL env vars look configured (no secrets returned).</summary>
    [HttpGet("health/config")]
    public IActionResult Config() =>
        Ok(new
        {
            openAiConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "(default gpt-4o-mini)",
            mysqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "(missing)",
            mysqlDatabase = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "(missing)",
            mysqlSslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE") ?? "(default Preferred)",
        });

    /// <summary>Runs <c>SELECT 1</c> against the configured MySQL — use on Railway to verify DB wiring.</summary>
    [HttpGet("health/db")]
    public async Task<IActionResult> DbCheck(CancellationToken ct)
    {
        try
        {
            await using var c = db.CreateConnection();
            await c.OpenAsync(ct);
            await using var cmd = new MySqlCommand("SELECT 1", c);
            var one = await cmd.ExecuteScalarAsync(ct);
            return Ok(new { status = "ok", scalar = one });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "error", error = ex.Message });
        }
    }
}
