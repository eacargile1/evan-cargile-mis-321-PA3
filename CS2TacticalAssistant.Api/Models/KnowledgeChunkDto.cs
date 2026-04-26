namespace CS2TacticalAssistant.Api.Models;

public sealed record KnowledgeChunkDto(
    ulong Id,
    string Category,
    string Title,
    string Content,
    string? Tags,
    double? Score);
