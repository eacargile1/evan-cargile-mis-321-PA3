using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public interface IMatchService
{
    Task<IReadOnlyList<ProMatchDto>> GetUpcomingAsync(
        string? tournamentContains,
        bool onlyMatchTodayUtc = true,
        CancellationToken ct = default);
}
