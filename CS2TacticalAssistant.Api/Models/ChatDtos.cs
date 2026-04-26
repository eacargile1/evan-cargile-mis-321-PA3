namespace CS2TacticalAssistant.Api.Models;

public sealed class ChatRequest
{
    public string Message { get; set; } = "";
    public ulong? UserId { get; set; }
}

public sealed class ChatResponse
{
    public string Reply { get; set; } = "";
    public IReadOnlyList<KnowledgeChunkDto> SourcesUsed { get; set; } = Array.Empty<KnowledgeChunkDto>();
    public IReadOnlyList<string> ToolCallsUsed { get; set; } = Array.Empty<string>();
}
