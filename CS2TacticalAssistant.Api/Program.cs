using CS2TacticalAssistant.Api.Services;
using DotNetEnv;
using Microsoft.Extensions.FileProviders;
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
// Published layout: DLL and wwwroot sit next to each other under AppContext.BaseDirectory.
// Use an explicit PhysicalFileProvider so static files work even if IWebHostEnvironment.WebRootPath is wrong on the host.
{
    var webRootPhysical = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
    if (!Directory.Exists(webRootPhysical))
        app.Logger.LogError("wwwroot missing at {Path} (BaseDirectory={Base})", webRootPhysical, AppContext.BaseDirectory);
    else
    {
        var files = new PhysicalFileProvider(webRootPhysical);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    }
}
app.UseAuthorization();
app.MapControllers();

// Health / diagnostics as minimal routes (guaranteed registration; avoids controller routing issues on some hosts).
app.MapGet(
    "/api/health",
    () =>
        Results.Json(new
        {
            status = "ok",
            timeUtc = DateTime.UtcNow,
        }));
app.MapGet(
    "/api/health/config",
    () =>
    {
        static string? EnvPick(params string[] keys)
        {
            foreach (var k in keys)
            {
                var v = Environment.GetEnvironmentVariable(k);
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }

            return null;
        }

        static string? FirstNonEmptyUrl()
        {
            foreach (var k in new[] { "MYSQL_URL", "MYSQL_PUBLIC_URL" })
            {
                var v = Environment.GetEnvironmentVariable(k);
                if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
            }

            return null;
        }

        static string? UserFromMysqlUrl(string rawUrl)
        {
            if (!rawUrl.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase)) return null;
            var uri = new Uri(
                rawUrl.Replace("mysql://", "http://", StringComparison.OrdinalIgnoreCase),
                UriKind.Absolute);
            var ui = uri.UserInfo;
            var colon = ui.IndexOf(':');
            return colon >= 0 ? Uri.UnescapeDataString(ui[..colon]) : Uri.UnescapeDataString(ui);
        }

        var preferSplit = string.Equals(Environment.GetEnvironmentVariable("MYSQL_PREFER_SPLIT_ENV")?.Trim(), "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("MYSQL_PREFER_SPLIT_ENV")?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        var rawUrl = FirstNonEmptyUrl();
        var usesUrl = !preferSplit
            && !string.IsNullOrWhiteSpace(rawUrl)
            && rawUrl.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase);
        var userResolved = usesUrl ? UserFromMysqlUrl(rawUrl!) : EnvPick("MYSQL_USER", "MYSQLUSER");
        string mode;
        if (preferSplit)
            mode = "split_env_forced_by_MYSQL_PREFER_SPLIT_ENV";
        else
        {
            var u1 = Environment.GetEnvironmentVariable("MYSQL_URL")?.Trim();
            if (!string.IsNullOrWhiteSpace(u1) && u1.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase))
                mode = "MYSQL_URL";
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYSQL_PUBLIC_URL")?.Trim())
                     && Environment.GetEnvironmentVariable("MYSQL_PUBLIC_URL")!.Trim()
                         .StartsWith("mysql://", StringComparison.OrdinalIgnoreCase))
                mode = "MYSQL_PUBLIC_URL";
            else
                mode = "split_env";
        }

        return Results.Json(
            new
            {
                openAiConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                openAiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "(default gpt-4o-mini)",
                mysqlConnectionMode = mode,
                mysqlUserResolvedForDb = userResolved ?? "(missing)",
                mysqlHost = EnvPick("MYSQL_HOST", "MYSQLHOST") ?? "(missing)",
                mysqlPort = EnvPick("MYSQL_PORT", "MYSQLPORT") ?? "(missing)",
                mysqlUser = EnvPick("MYSQL_USER", "MYSQLUSER") ?? "(missing)",
                mysqlPasswordSet = !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("MYSQL_PASSWORD")
                    ?? Environment.GetEnvironmentVariable("MYSQLPASSWORD")),
                mysqlDatabase = EnvPick("MYSQL_DATABASE", "MYSQLDATABASE") ?? "(missing)",
                mysqlSslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE") ?? "(default Preferred)",
                mysqlUrlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYSQL_URL")),
                mysqlPublicUrlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYSQL_PUBLIC_URL")),
                mysqlPreferSplitEnv = string.Equals(
                    Environment.GetEnvironmentVariable("MYSQL_PREFER_SPLIT_ENV")?.Trim(),
                    "1",
                    StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        Environment.GetEnvironmentVariable("MYSQL_PREFER_SPLIT_ENV")?.Trim(),
                        "true",
                        StringComparison.OrdinalIgnoreCase),
                note =
                    "DatabaseService uses MYSQL_URL (else MYSQL_PUBLIC_URL) unless MYSQL_PREFER_SPLIT_ENV=1; then MYSQLHOST/MYSQLPASSWORD/… only. No hardcoded DB user.",
            });
    });
app.MapGet(
    "/api/health/db",
    async (IDatabaseService db, CancellationToken ct) =>
    {
        try
        {
            await using var c = db.CreateConnection();
            await c.OpenAsync(ct);
            await using var cmd = new MySqlCommand("SELECT 1", c);
            var one = await cmd.ExecuteScalarAsync(ct);
            return Results.Json(new { status = "ok", scalar = one });
        }
        catch (Exception ex)
        {
            return Results.Json(new { status = "error", error = ex.Message }, statusCode: 503);
        }
    });

// Railway / minimal-hosting: endpoint routing can bypass UseStaticFiles for extension paths.
// Serve critical static assets explicitly so CSS/JS always load (catch-all below would 404 them).
{
    var wroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
    var imagesRoot = Path.GetFullPath(Path.Combine(wroot, "images"));

    static string ContentTypeForImage(string ext) =>
        ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "application/octet-stream";

    app.MapGet("/css/app.css", () =>
    {
        var f = Path.Combine(wroot, "css", "app.css");
        return File.Exists(f) ? Results.File(f, "text/css") : Results.NotFound();
    });
    app.MapGet("/js/app.js", () =>
    {
        var f = Path.Combine(wroot, "js", "app.js");
        return File.Exists(f) ? Results.File(f, "application/javascript") : Results.NotFound();
    });
    app.MapGet("/images/{**path}", (string path) =>
    {
        if (string.IsNullOrEmpty(path) || path.Contains("..", StringComparison.Ordinal))
            return Results.BadRequest();
        var full = Path.GetFullPath(Path.Combine(imagesRoot, path));
        if (!full.StartsWith(imagesRoot, StringComparison.Ordinal) || full.Equals(imagesRoot, StringComparison.Ordinal))
            return Results.NotFound();
        return File.Exists(full)
            ? Results.File(full, ContentTypeForImage(Path.GetExtension(full)))
            : Results.NotFound();
    });
}

// Confirms static assets exist in the running container (Railway / Docker debugging).
app.MapGet(
    "/api/health/static",
    () =>
    {
        var webRootPhysical = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        var css = Path.Combine(webRootPhysical, "css", "app.css");
        var js = Path.Combine(webRootPhysical, "js", "app.js");
        return Results.Json(
            new
            {
                baseDirectory = AppContext.BaseDirectory,
                contentRoot = app.Environment.ContentRootPath,
                webRootEnv = app.Environment.WebRootPath,
                wwwrootPhysical = webRootPhysical,
                wwwrootExists = Directory.Exists(webRootPhysical),
                cssExists = File.Exists(css),
                jsExists = File.Exists(js),
            });
    });

// SPA: serve index.html only when **no other endpoint matched** (MapFallback — not a greedy catch-all MapGet,
// which can steal /api/* from attribute-routed controllers and return 404).
{
    var wroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
    var index = Path.Combine(wroot, "index.html");
    app.MapFallback((HttpContext http) =>
    {
        if (http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(404);
        if (Path.HasExtension(http.Request.Path.Value))
            return Results.StatusCode(404);
        if (!File.Exists(index))
        {
            app.Logger.LogError("index.html not found at {Path}", index);
            return Results.StatusCode(500);
        }

        return Results.File(index, "text/html");
    });
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
