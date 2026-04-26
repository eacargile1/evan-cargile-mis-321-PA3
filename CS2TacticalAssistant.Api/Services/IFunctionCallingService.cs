namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// --- Function calling (tool execution) ---
/// Dispatches OpenAI tool names to domain services. Tool schemas are registered in <see cref="LlmService"/>.
/// </summary>
public interface IFunctionCallingService
{
    Task<string> ExecuteToolAsync(string functionName, string argumentsJson, CancellationToken ct = default);
}
