using System.Text.Json.Serialization;

namespace CS2TacticalAssistant.Api.Models;

public sealed class ProMatchDto
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("tournament")]
    public string Tournament { get; set; } = "";

    [JsonPropertyName("teamA")]
    public string TeamA { get; set; } = "";

    [JsonPropertyName("teamB")]
    public string TeamB { get; set; } = "";

    [JsonPropertyName("matchTimeUtc")]
    public DateTime? MatchTimeUtc { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("matchPageUrl")]
    public string? MatchPageUrl { get; set; }
}
