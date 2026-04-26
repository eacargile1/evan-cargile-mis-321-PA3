namespace CS2TacticalAssistant.Api.Models;

public sealed class SaveStratRequest
{
    public ulong UserId { get; set; } = 1;
    public string Title { get; set; } = "";
    public object Payload { get; set; } = new { };
}

public sealed class SavedStratDto
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public string Title { get; set; } = "";
    public string BodyJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
