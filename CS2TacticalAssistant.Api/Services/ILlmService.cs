using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

public interface ILlmService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);
}
