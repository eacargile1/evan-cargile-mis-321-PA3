using System.Text.Json.Nodes;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>Deterministic round-plan generator used by function calling + /api demos.</summary>
public sealed class StrategyService : IStrategyService
{
    public JsonObject GenerateRoundStrategy(string map, string side, string roundType, string goal)
    {
        map = string.IsNullOrWhiteSpace(map) ? "Unknown" : map.Trim();
        var s0 = side.Trim().ToUpperInvariant();
        side = s0 is "CT" or "T" ? s0 : "T";
        roundType = roundType.Trim().ToLowerInvariant().Replace('_', ' ');
        goal = goal.Trim().ToLowerInvariant();

        var buy = roundType switch
        {
            "pistol" => side == "T"
                ? "2x smoke 2x flash 1x molly split; kevlar on 3 strongest aimers, 2 naked utility bots."
                : "Full utility kit spread; kevlar on 4, one kit player if you can afford without starving nades.",
            "eco" => "P250/Deagle hero optional; otherwise save to 5-man rifle round — one scout/UMP only if you have a free pick setup.",
            "force" => side == "T"
                ? "MAC-10/MP9 core + 1 smoke + 2 flash minimum; aim to trade banana/mid fast."
                : "Famas/M4 mix if money allows; otherwise MP9 hold stacks with full nade set.",
            "full buy" or "fullbuy" => side == "T"
                ? "AK + full utility; AWPer only if economy stable post-support buys."
                : "M4/A1-S + full utility; AWP anchor on strongest solo site.",
            _ => "Light buy coordinated with team — avoid mixed full + eco buys."
        };

        var roles = new JsonArray
        {
            JsonValue.Create($"Entry ({map} {side}) — first contact, calls micro-adjust")!,
            JsonValue.Create("Support — set utility sequence and refrag trades")!,
            JsonValue.Create("IGL — mid-round call based on first 20s info")!,
            JsonValue.Create(side == "T" ? "Lurk — opposite site timing + rotate punish" : "Rotator — early info, decisive commit on bomb audio")!,
            JsonValue.Create(side == "T" ? "AWPer / rifle anchor — space or late pick" : "Anchor — site stall + time play")!
        };

        var utility = goal switch
        {
            "execute" => "Smoke choke + flash swing + molly common plant; keep one molly for post-plant deny.",
            "retake" => "Save smokes for site retake lanes; flash over site boxes; molly default plant.",
            "mid control" => "Early mid nades, deep smoke timing, one flash for info peek.",
            "save" => "No utility spend; play timing and spacing only.",
            _ => "Default utility for map control — do not overthrow before commit."
        };

        var timing = goal == "execute"
            ? "Utility fly at -0:25 to -0:20; hit site at -0:12 with trade order locked."
            : "First 0:40 map control; execute window opens when lurk pins rotation or utility advantage spikes.";

        var steps = new JsonArray
        {
            JsonValue.Create($"1) Default {goal} on {map} ({side}) — sound discipline first 20s.")!,
            JsonValue.Create("2) Win mid / parallel pressure — first duel sets pace for the round.")!,
            JsonValue.Create("3) Execute or pivot on info — no silent deaths; trade every opener.")!,
            JsonValue.Create("4) Post-plant / save — call early if time is low or crossfires are set.")!
        };

        return new JsonObject
        {
            ["map"] = map,
            ["side"] = side,
            ["round_type"] = roundType,
            ["goal"] = goal,
            ["recommended_buy"] = buy,
            ["player_roles"] = roles,
            ["utility_plan"] = utility,
            ["timing"] = timing,
            ["step_by_step_round_strategy"] = steps
        };
    }
}
