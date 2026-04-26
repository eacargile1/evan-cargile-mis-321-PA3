using System.Text.Json;
using System.Text.Json.Nodes;
using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public sealed class FunctionCallingService(
    IStrategyService strategy,
    IEconomyService economy,
    ILineupService lineups,
    IMatchService matches) : IFunctionCallingService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<string> ExecuteToolAsync(string functionName, string argumentsJson, CancellationToken ct = default)
    {
        using var doc = string.IsNullOrWhiteSpace(argumentsJson)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;

        switch (functionName)
        {
            case "generate_round_strategy":
            {
                var map = GetStr(root, "map");
                var side = GetStr(root, "side");
                var roundType = GetStr(root, "round_type");
                var goal = GetStr(root, "goal");
                var obj = strategy.GenerateRoundStrategy(map, side, roundType, goal);
                return obj.ToJsonString();
            }
            case "explain_economy_decision":
            {
                var req = JsonSerializer.Deserialize<EconomyExplainRequest>(argumentsJson, JsonOpts)
                          ?? new EconomyExplainRequest();
                var dto = economy.Explain(req);
                return JsonSerializer.Serialize(dto, JsonOpts);
            }
            case "search_lineups":
            {
                var map = GetStr(root, "map");
                var site = root.TryGetProperty("site", out var s) ? s.GetString() : null;
                var gt = root.TryGetProperty("grenade_type", out var g) ? g.GetString() : null;
                var side = root.TryGetProperty("side", out var sd) ? sd.GetString() : null;
                var rows = await lineups.SearchAsync(map, site, gt, side, ct);
                return JsonSerializer.Serialize(rows, JsonOpts);
            }
            case "get_today_matches":
            {
                var t = root.TryGetProperty("tournament_name", out var tn) ? tn.GetString() : null;
                var rows = await matches.GetUpcomingAsync(t, onlyMatchTodayUtc: true, ct);
                return JsonSerializer.Serialize(rows, JsonOpts);
            }
            default:
                return JsonSerializer.Serialize(new { error = "unknown_tool", functionName });
        }
    }

    private static string GetStr(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) ? (p.GetString() ?? "") : "";
}
