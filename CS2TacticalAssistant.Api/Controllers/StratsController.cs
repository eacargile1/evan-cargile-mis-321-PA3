using System.Text.Json;
using CS2TacticalAssistant.Api.Models;
using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/strats")]
public sealed class StratsController(IDatabaseService db) : ControllerBase
{
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveStratRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { error = "Title required." });
        var bodyJson = JsonSerializer.Serialize(req.Payload);

        try
        {
            await using var conn = db.CreateConnection();
            await conn.OpenAsync(ct);
            const string sql = """
                INSERT INTO saved_strats (user_id, title, body) VALUES (@u, @t, @b)
                """;
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", req.UserId);
            cmd.Parameters.AddWithValue("@t", req.Title.Trim());
            var pb = new MySqlParameter("@b", MySqlDbType.JSON) { Value = bodyJson };
            cmd.Parameters.Add(pb);
            await cmd.ExecuteNonQueryAsync(ct);
            return Ok(new { ok = true });
        }
        catch (MySqlException ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedStratDto>>> List([FromQuery] ulong userId = 1, CancellationToken ct = default)
    {
        try
        {
            await using var conn = db.CreateConnection();
            await conn.OpenAsync(ct);
            const string sql = """
                SELECT id, user_id, title, CAST(body AS CHAR) AS body_json, created_at
                FROM saved_strats WHERE user_id=@u ORDER BY id DESC LIMIT 50
                """;
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", userId);
            var list = new List<SavedStratDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var oBody = reader.GetOrdinal("body_json");
                list.Add(new SavedStratDto
                {
                    Id = reader.GetUInt64("id"),
                    UserId = reader.GetUInt64("user_id"),
                    Title = reader.GetString("title"),
                    BodyJson = reader.IsDBNull(oBody) ? "{}" : reader.GetString(oBody),
                    CreatedAt = reader.GetDateTime("created_at")
                });
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}
