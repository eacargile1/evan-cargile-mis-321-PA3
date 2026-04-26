using CS2TacticalAssistant.Api.Models;
using CS2TacticalAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CS2TacticalAssistant.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ILlmService llm) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await llm.ChatAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // MySQL missing, network, OpenAI errors, etc. — surface message for TA debugging.
            return StatusCode(502, new { error = ex.Message });
        }
    }
}
