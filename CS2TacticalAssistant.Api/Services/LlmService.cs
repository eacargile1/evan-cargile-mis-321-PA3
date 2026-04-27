using System.Reflection;
using System.Text;
using System.Text.Json;
using CS2TacticalAssistant.Api.Models;
using MySqlConnector;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// Orchestrates: (1) <b>RAG retrieval</b> from MySQL, (2) <b>LLM</b> chat completion via OpenAI,
/// (3) <b>function calling</b> loop executing backend tools. Each stage is marked in code for demos.
/// </summary>
public sealed class LlmService : ILlmService
{
    private readonly ChatClient? _chatClient;
    /// <summary>OpenAI model id used for <see cref="ChatClient"/> and forced onto each <see cref="ChatCompletionOptions"/>.</summary>
    private readonly string _model;
    private readonly string _openAiEndpoint;
    private readonly IRagService _rag;
    private readonly IFunctionCallingService _functionCalling;
    private readonly IDatabaseService _database;

    private static readonly ChatTool[] CoachTools =
    [
        ChatTool.CreateFunctionTool(
            functionName: "generate_round_strategy",
            functionDescription:
            "Build a structured round plan: buys, roles, utility, timing, and step-by-step strategy for CS2.",
            functionParameters: BinaryData.FromBytes("""
            {
              "type": "object",
              "properties": {
                "map": { "type": "string", "description": "Map name e.g. Mirage, Inferno, Ancient" },
                "side": { "type": "string", "enum": ["T", "CT"] },
                "round_type": { "type": "string", "enum": ["pistol", "eco", "force", "full buy"] },
                "goal": { "type": "string", "enum": ["default", "execute", "retake", "save", "lurk", "mid control", "site hit"] }
              },
              "required": ["map", "side", "round_type", "goal"]
            }
            """u8.ToArray())),
        ChatTool.CreateFunctionTool(
            functionName: "explain_economy_decision",
            functionDescription: "Explain buy/save decision from team money, loss bonus, side, round, and saved weapons.",
            functionParameters: BinaryData.FromBytes("""
            {
              "type": "object",
              "properties": {
                "team_money": { "type": "integer" },
                "loss_bonus": { "type": "integer" },
                "side": { "type": "string", "enum": ["T", "CT"] },
                "round_number": { "type": "integer" },
                "weapons_saved": { "type": "array", "items": { "type": "string" } }
              },
              "required": ["team_money", "loss_bonus", "side", "round_number", "weapons_saved"]
            }
            """u8.ToArray())),
        ChatTool.CreateFunctionTool(
            functionName: "search_lineups",
            functionDescription: "Search grenade lineups from the MySQL lineup_library table.",
            functionParameters: BinaryData.FromBytes("""
            {
              "type": "object",
              "properties": {
                "map": { "type": "string" },
                "site": { "type": "string", "description": "A, B, mid, etc." },
                "grenade_type": { "type": "string", "enum": ["smoke", "flash", "molly", "HE"] },
                "side": { "type": "string", "enum": ["T", "CT"] }
              },
              "required": ["map", "site", "grenade_type", "side"]
            }
            """u8.ToArray()))
        // get_today_matches omitted until HLTV bridge is deployed (see Matches tab / HLTV_BRIDGE_URL).
    ];

    public LlmService(IRagService rag, IFunctionCallingService functionCalling, IDatabaseService database)
    {
        _rag = rag;
        _functionCalling = functionCalling;
        _database = database;

        // --- LLM usage: OpenAI API key from environment (never hardcode) ---
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        // Railway often defines OPENAI_MODEL with an empty value; ?? only handles null, not "".
        var modelEnv = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        _model = string.IsNullOrWhiteSpace(modelEnv) ? "gpt-4o-mini" : modelEnv.Trim();
        _openAiEndpoint = "https://api.openai.com/v1";
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(_openAiEndpoint)
            };
            _chatClient = new ChatClient(model: _model, credential: new ApiKeyCredential(apiKey), options: options);
        }
    }

    /// <summary>
    /// OpenAI SDK merges request options with <c>options.Model ??= clientModel</c>. If <c>Model</c> is the empty string,
    /// it is not null-coalesced and the API returns "you must provide a model parameter". Force a non-empty model on each request.
    /// </summary>
    private ChatCompletionOptions CreateChatCompletionOptions()
    {
        var o = new ChatCompletionOptions();
        foreach (var t in CoachTools)
            o.Tools.Add(t);
        ChatCompletionOptionsModel.Set(o, _model);
        return o;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        if (_chatClient is null)
            throw new InvalidOperationException("OPENAI_API_KEY is not set. Copy example.env and export variables.");

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.");

        // --- RAG retrieval: pull knowledge chunks from MySQL for grounded prompting ---
        var sources = await _rag.SearchAsync(request.Message, take: 8, ct);
        var ragBlock = new StringBuilder();
        ragBlock.AppendLine("Knowledge base excerpts (cite mentally, do not invent map-specific facts beyond these):");
        foreach (var s in sources)
            ragBlock.AppendLine($"[{s.Category}] {s.Title}: {s.Content}");

        var system = $"""
            You are **CS2 Tactical Assistant**, an esports coach / analyst for Counter-Strike 2.
            Tone: practical, concise, team-focused. Prefer numbered steps for executes.
            When structured data helps (lineups, economy math, full round plans), call the provided tools.
            After tools return, synthesize a clear answer for the player — do not dump raw JSON.
            {ragBlock}
            """;

        List<ChatMessage> messages =
        [
            new SystemChatMessage(system),
            new UserChatMessage(request.Message)
        ];

        var toolCallsUsed = new List<string>();
        string reply = "";

        for (var round = 0; round < 8; round++)
        {
            // --- LLM usage: model completion request ---
            var requestOptions = CreateChatCompletionOptions();
            var requestModel = ChatCompletionOptionsModel.Get(requestOptions);
            ChatCompletion completion;
            try
            {
                completion = (await _chatClient.CompleteChatAsync(messages, requestOptions, ct)).Value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"OpenAI chat call failed (endpoint='{_openAiEndpoint}', clientModel='{_model}', requestModel='{requestModel ?? "(null)"}'). {ex.Message}",
                    ex);
            }

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    reply = string.Concat(completion.Content.Select(static p => p.Text ?? ""));
                    messages.Add(new AssistantChatMessage(completion));
                    round = 999;
                    break;

                case ChatFinishReason.ToolCalls:
                    messages.Add(new AssistantChatMessage(completion));
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        toolCallsUsed.Add(toolCall.FunctionName);
                        var args = toolCall.FunctionArguments?.ToString() ?? "{}";
                        // --- Function calling: execute C# tool handlers against MySQL / heuristics ---
                        var toolOutput = await _functionCalling.ExecuteToolAsync(toolCall.FunctionName, args, ct);
                        messages.Add(new ToolChatMessage(toolCall.Id, toolOutput));
                    }
                    break;

                default:
                    reply = string.Concat(completion.Content.Select(static p => p.Text ?? ""));
                    messages.Add(new AssistantChatMessage(completion));
                    round = 999;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = toolCallsUsed.Count > 0
                ? "Tools returned data but the model hit the max tool round limit without a final summary — try a shorter question or repeat."
                : "No assistant text returned — check OPENAI_API_KEY, model name, or API errors.";
        }

        await PersistChatAsync(request.UserId, request.Message, reply, sources, toolCallsUsed, ct);

        return new ChatResponse
        {
            Reply = reply,
            SourcesUsed = sources,
            ToolCallsUsed = toolCallsUsed.Distinct().ToList()
        };
    }

    private async Task PersistChatAsync(
        ulong? userId,
        string userMsg,
        string assistantMsg,
        IReadOnlyList<KnowledgeChunkDto> sources,
        List<string> tools,
        CancellationToken ct)
    {
        await using var conn = _database.CreateConnection();
        await conn.OpenAsync(ct);
        var meta = JsonSerializer.Serialize(new { sources = sources.Select(s => s.Id).ToList(), tools });
        const string sql = """
            INSERT INTO chat_logs (user_id, role, content, meta) VALUES (@uid, @role, @content, CAST(@meta AS JSON))
            """;
        async Task Ins(string role, string content)
        {
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", userId.HasValue ? userId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@meta", meta);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await Ins("user", userMsg);
        await Ins("assistant", assistantMsg);
    }
}

/// <summary>OpenAI exposes <see cref="ChatCompletionOptions"/> model id via an internal property; set it so requests always include <c>model</c>.</summary>
file static class ChatCompletionOptionsModel
{
    private static readonly PropertyInfo? ModelProperty = typeof(ChatCompletionOptions).GetProperty(
        "Model",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static void Set(ChatCompletionOptions options, string model) => ModelProperty?.SetValue(options, model);
    public static string? Get(ChatCompletionOptions options) => ModelProperty?.GetValue(options) as string;
}
