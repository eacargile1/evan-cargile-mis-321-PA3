namespace CS2TacticalAssistant.Api.Models;

public sealed class EconomyExplainRequest
{
    public int TeamMoney { get; set; }
    public int LossBonus { get; set; }
    public string Side { get; set; } = "T";
    public int RoundNumber { get; set; }
    public List<string> WeaponsSaved { get; set; } = new();
}

public sealed class EconomyExplanationDto
{
    public string BuyRecommendation { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public List<string> SuggestedWeaponsUtility { get; set; } = new();
}
