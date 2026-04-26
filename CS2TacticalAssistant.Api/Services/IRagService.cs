using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public interface IRagService
{
    Task<IReadOnlyList<KnowledgeChunkDto>> SearchAsync(string query, int take = 8, CancellationToken ct = default);
}
