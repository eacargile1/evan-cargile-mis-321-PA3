using System.Net.Http;
using System.Text.Json;
using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// Upcoming / live match rows from the local <c>hltv-bridge</c> process (gigobyte <c>hltv</c> npm package).
/// Set <c>HLTV_BRIDGE_URL</c> (default <c>http://127.0.0.1:3847</c>) and run <c>npm start</c> in <c>hltv-bridge/</c>.
/// </summary>
public sealed class HltvBridgeMatchService(IHttpClientFactory httpFactory) : IMatchService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ProMatchDto>> GetUpcomingAsync(
        string? tournamentContains,
        bool onlyMatchTodayUtc = true,
        CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("hltv");
        HttpResponseMessage res;
        try
        {
            res = await client.GetAsync("matches", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Bridge not running or network — degrade gracefully for UI / demos
            return new[] { new ProMatchDto
            {
                Id = 0, TeamA = "—", TeamB = "—", Tournament = "HLTV bridge offline",
                MatchTimeUtc = DateTime.UtcNow, Status = "error", Notes = $"Start: cd hltv-bridge && npm i && npm start. ({ex.Message})",
            }};
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var msg = UnwrapBridgeErrorBody(json) ?? (json.Length > 400 ? json[..400] : json);
            return
            [
                new ProMatchDto
                {
                    Id = 0, TeamA = "—", TeamB = "—", Tournament = "HLTV fetch failed", MatchTimeUtc = DateTime.UtcNow,
                    Status = "error", Notes = msg
                }
            ];
        }

        var all = JsonSerializer.Deserialize<List<ProMatchDto>>(json, JsonOpts) ?? new List<ProMatchDto>();
        if (all.Count == 0)
        {
            return new[]
            {
                new ProMatchDto
                {
                    Id = 0,
                    TeamA = "—",
                    TeamB = "—",
                    Tournament = "No match rows",
                    MatchTimeUtc = null,
                    Status = "—",
                    Notes = "This list is empty. Start the bridge (hltv-bridge: npm i && node server.mjs) on port 3847, set HLTV_BRIDGE_URL, or HLTV may be temporarily empty.",
                },
            };
        }

        if (!string.IsNullOrWhiteSpace(tournamentContains))
        {
            var t = tournamentContains.Trim();
            all = all
                .Where(m => m.Tournament.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || m.TeamA.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || m.TeamB.Contains(t, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (onlyMatchTodayUtc)
        {
            // Same calendar day as the server clock (API uses UTC) — HLTV schedule is global.
            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(1);
            all = all
                .Where(m => m.MatchTimeUtc is { } ts && ts >= start && ts < end)
                .ToList();
        }

        return all; // can be [] when the bridge has data but nothing falls in today’s window
    }

    /// <summary>Bridge returns JSON like <c>{"error":"…"}</c> for 4xx/5xx — show the message, not raw JSON.</summary>
    private static string? UnwrapBridgeErrorBody(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        if (json.TrimStart()[0] is not '{' and not '[') return null;
        try
        {
            using var d = JsonDocument.Parse(json);
            if (d.RootElement.TryGetProperty("error", out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
