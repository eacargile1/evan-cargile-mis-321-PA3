using System.Text.Json.Nodes;

namespace CS2TacticalAssistant.Api.Services;

public interface IStrategyService
{
    JsonObject GenerateRoundStrategy(string map, string side, string roundType, string goal);
}
