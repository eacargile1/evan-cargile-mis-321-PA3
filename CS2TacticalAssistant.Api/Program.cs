using CS2TacticalAssistant.Api.Services;
using DotNetEnv;
using MySqlConnector;

// .NET does not load .env by default. Prefer: repo .env one level up from build output, else walk CWD upward.
// Use .env for real secrets; example.env is a template only.
{
    var fromBin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"));
    if (File.Exists(fromBin))
        Env.Load(fromBin);
    else
    {
        var dir = Directory.GetCurrentDirectory();
        for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(dir); depth++)
        {
            var localEnv = Path.Combine(dir, ".env");
            var example = Path.Combine(dir, "example.env");
            if (File.Exists(localEnv))
            {
                Env.Load(localEnv);
                break;
            }

            if (File.Exists(example))
            {
                Env.Load(example);
                break;
            }

            dir = Directory.GetParent(dir)?.FullName ?? "";
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// --- Hosted deployment setup: Heroku sets PORT; map it for Kestrel when present. ---
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

// HLTV bridge (gigobyte/hltv via Node): default http://127.0.0.1:3847/matches — see hltv-bridge/README
{
    var h = Environment.GetEnvironmentVariable("HLTV_BRIDGE_URL")?.Trim() ?? "http://127.0.0.1:3847";
    if (!h.EndsWith('/')) h += "/";
    builder.Services.AddHttpClient("hltv", c =>
    {
        c.BaseAddress = new Uri(h);
        c.Timeout = TimeSpan.FromSeconds(45);
    });
}

// Steam public Web API (ISteamNews.GetNewsForApp) — no API key, CS2 app 730
builder.Services.AddHttpClient("steam", c =>
{
    c.BaseAddress = new Uri("https://api.steampowered.com/");
    c.Timeout = TimeSpan.FromSeconds(45);
    c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "CS2TacticalAssistant/1.0");
    c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
}).ConfigurePrimaryHttpMessageHandler(_ => new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All,
});
builder.Services.AddSingleton<SteamGameNewsService>();

// --- Hosted deployment setup: register app services (DB + OpenAI keys come from env — never from source). ---
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IStrategyService, StrategyService>();
builder.Services.AddSingleton<IEconomyService, EconomyService>();
builder.Services.AddSingleton<IMatchService, HltvBridgeMatchService>();
builder.Services.AddSingleton<ILineupService, LineupService>();
builder.Services.AddSingleton<IFunctionCallingService, FunctionCallingService>();
builder.Services.AddSingleton<ILlmService, LlmService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

// SPA: serve index.html for non-file routes. Never map HTML onto /api/* (missing or unknown API paths must not return index.html,
// or fetch("/api/...") gets <!doctype> and JSON.parse throws).
{
    var env = app.Environment;
    var wroot = string.IsNullOrEmpty(env.WebRootPath) ? Path.Combine(env.ContentRootPath, "wwwroot") : env.WebRootPath;
    var index = Path.GetFullPath(Path.Combine(wroot, "index.html"));
    app.MapGet(
        "/{**path}",
        (HttpContext http) =>
        {
            // Never serve SPA HTML for static asset-like paths (e.g. /js/app.js, /css/app.css).
            // If a file is missing, return 404 so browsers don't try to parse HTML as JS/CSS.
            if (Path.HasExtension(http.Request.Path.Value))
                return Results.StatusCode(404);

            if (http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                return Results.StatusCode(404);
            if (!File.Exists(index))
            {
                app.Logger.LogError("index.html not found at {Path}", index);
                return Results.StatusCode(500);
            }

            return Results.File(index, "text/html");
        })
        // Prefer MapControllers and other specific routes; only then serve index for deep links to the SPA.
        .WithOrder(100_000);
}

// Ensure FK parent exists for user_id=1 (saved strats, reminders) when DB is fresh
try
{
    var db = app.Services.GetRequiredService<IDatabaseService>();
    await using var c = db.CreateConnection();
    await c.OpenAsync();
    await using var ensure = new MySqlCommand(
        "INSERT IGNORE INTO users (id, username, email) VALUES (1, 'demo_coach', 'demo@example.com')",
        c);
    await ensure.ExecuteNonQueryAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "DB bootstrap: could not ensure demo user — apply schema/seed and check MYSQL_* in .env.");
}

await app.RunAsync();
