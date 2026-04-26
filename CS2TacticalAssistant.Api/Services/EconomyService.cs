using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public sealed class EconomyService : IEconomyService
{
    public EconomyExplanationDto Explain(EconomyExplainRequest req)
    {
        var m = req.TeamMoney;
        var loss = Math.Max(0, req.LossBonus);
        var side = req.Side.Trim().ToUpperInvariant() is "CT" or "T" ? req.Side.Trim().ToUpperInvariant() : "T";
        var saved = req.WeaponsSaved?.Count ?? 0;

        if (req.RoundNumber <= 1)
        {
            return new EconomyExplanationDto
            {
                BuyRecommendation = "Pistol round — buy util only with a practiced plan; otherwise armor/value pistols.",
                Reasoning = "Opening economy is about trades and bomb plants, not perfect loadouts.",
                RiskLevel = "low",
                SuggestedWeaponsUtility = ["Kevlar", "Smoke", "Flashbang"]
            };
        }

        if (m >= 4300)
        {
            return new EconomyExplanationDto
            {
                BuyRecommendation = "Full buy: rifle + full utility (+ AWP only on designated anchor with team cover).",
                Reasoning = $"At ~${m} you can afford rifle/nade/kevlar lines; loss bonus ${loss} is safety net next round.",
                RiskLevel = "low",
                SuggestedWeaponsUtility = side == "T"
                    ? ["AK-47", "Smoke x2", "Flash x2", "Molotov", "HE"]
                    : ["M4A1-S / M4A4", "Smoke x2", "Flash x2", "Molotov", "HE", "Defuse kit"]
            };
        }

        if (req.RoundNumber == 2 && m < 3500 && side == "T")
        {
            return new EconomyExplanationDto
            {
                BuyRecommendation =
                    "After pistol loss: default is eco to round 3 rifles — force only if you have a punish read (light CT buy / weak site).",
                Reasoning =
                    $"You have ~${m} now with +${loss} loss context; coordinated MAC-10 can work but risks the snowball.",
                RiskLevel = "high",
                SuggestedWeaponsUtility = ["Team eco", "Or MAC-10 + smokes stack"]
            };
        }

        if (m is >= 2900 and < 4300)
        {
            return new EconomyExplanationDto
            {
                BuyRecommendation = "Force / half-buy territory — pick ONE plan for all five players.",
                Reasoning = $"~${m} is awkward: either commit SMG+util or save to next full threshold.",
                RiskLevel = "medium",
                SuggestedWeaponsUtility = side == "T"
                    ? ["Galil + reduced util", "MAC-10 rush package"]
                    : ["Famas", "MP9 anchor", "Full nades on B anchor"]
            };
        }

        if (saved >= 2 && m >= 2000)
        {
            return new EconomyExplanationDto
            {
                BuyRecommendation = "Re-buy around saved weapons — refill nades first, then kevlar.",
                Reasoning = "Saved rifles reduce effective cost of a full round.",
                RiskLevel = "medium",
                SuggestedWeaponsUtility = ["Refill smokes/flashes", "Kevlar", "Defuse kit (CT)"]
            };
        }

        return new EconomyExplanationDto
        {
            BuyRecommendation = "Eco / partial — pistols + one team smoke max unless you have a free pick setup.",
            Reasoning = $"~${m} is below clean rifle thresholds; loss bonus ${loss} means next loss may still float a buy.",
            RiskLevel = "low",
            SuggestedWeaponsUtility = ["P250", "Save for synchronized buy"]
        };
    }
}
