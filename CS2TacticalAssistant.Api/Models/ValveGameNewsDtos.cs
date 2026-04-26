using System.Text.Json.Serialization;

namespace CS2TacticalAssistant.Api.Models;

public sealed class ValveGameNewsResult
{
    [JsonPropertyName("items")]
    public IReadOnlyList<ValvePatchNoteItemDto> Items { get; init; } = Array.Empty<ValvePatchNoteItemDto>();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public sealed class ValvePatchNoteItemDto
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("postedAt")]
    public string PostedAt { get; init; } = "";

    [JsonPropertyName("excerpt")]
    public string Excerpt { get; init; } = "";

    /// <summary>Full post body as plain text (Valve/Steam <c>contents</c>, newlines between bullets).</summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; init; }
}
