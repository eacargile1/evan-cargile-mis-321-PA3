namespace CS2TacticalAssistant.Api.Models;

public sealed class LineupDto
{
    public ulong Id { get; set; }
    public string LineupName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string WhenToUse { get; set; } = "";
    public string Map { get; set; } = "";
    public string Site { get; set; } = "";
    public string GrenadeType { get; set; } = "";
    public string Side { get; set; } = "";
}
