using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController(IRagService rag) : ControllerBase
{
    /// <summary>Direct RAG search (same retrieval path as chat grounding).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "Query 'q' is required." });
        return Ok(await rag.SearchAsync(q, take: 12, ct));
    }
}
