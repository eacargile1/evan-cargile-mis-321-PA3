using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public interface IEconomyService
{
    EconomyExplanationDto Explain(EconomyExplainRequest req);
}
