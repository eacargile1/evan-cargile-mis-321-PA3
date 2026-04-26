using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public interface ILineupService
{
    Task<IReadOnlyList<LineupDto>> SearchAsync(string map, string? site, string? grenadeType, string? side, CancellationToken ct = default);
}
