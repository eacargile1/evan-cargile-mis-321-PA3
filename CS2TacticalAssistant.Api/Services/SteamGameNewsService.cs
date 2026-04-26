using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CS2TacticalAssistant.Api.Models;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>Official CS2 (Steam app 730) news: patch and release items via the public Steam Web API (no key).</summary>
public sealed class SteamGameNewsService(IHttpClientFactory httpFactory)
{
    private const int Cs2AppId = 730; // Counter-Strike 2 on Steam (same app id as legacy CS:GO)
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex TagStrip = new("<[^>]+>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public async Task<ValveGameNewsResult> GetRecentCs2NewsAsync(int count = 6, int excerptMax = 300, int bodyMax = 20_000, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("steam");
        // maxlength=0 → full text in response; we strip HTML and trim on the server
        var q =
            $"ISteamNews/GetNewsForApp/v2/?appid={Cs2AppId}&count={Math.Clamp(count, 1, 20)}&format=json&maxlength=0";
        try
        {
            var res = await client.GetAsync(q, ct);
            if (!res.IsSuccessStatusCode)
            {
                return new ValveGameNewsResult
                {
                    Error = $"Steam news returned {(int)res.StatusCode} — try again in a few minutes.",
                };
            }

            await using var s = await res.Content.ReadAsStreamAsync(ct);
            var raw = await JsonSerializer.DeserializeAsync<SteamNewsEnvelope>(s, JsonOpts, ct);
            var list = raw?.Appnews?.Newsitems;
            if (list == null || list.Count == 0)
            {
                return new ValveGameNewsResult { Error = "No news items in the Steam response." };
            }

            var items = new List<ValvePatchNoteItemDto>(list.Count);
            foreach (var n in list)
            {
                var u = n.Url?.Trim() ?? "";
                if (u.Length == 0) continue;
                var when = n.Date > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(n.Date).ToUniversalTime().ToString("MMM d, yyyy")
                    : "—";
                var title = (n.Title ?? "Update").Trim();
                if (string.IsNullOrEmpty(title)) title = "CS2 update";
                var body = ToPostBody(n.Contents, title, bodyMax);
                if (string.IsNullOrEmpty(body)) body = title;
                var excerpt = body.Length <= excerptMax
                    ? body
                    : string.Concat(body.AsSpan(0, Math.Min(excerptMax - 1, body.Length)), "…");
                items.Add(new ValvePatchNoteItemDto
                {
                    Title = title,
                    Url = u,
                    PostedAt = when,
                    Excerpt = excerpt,
                    Body = body,
                    Source = string.IsNullOrEmpty(n.Feedname) ? null : n.Feedname,
                });
            }

            if (items.Count == 0)
            {
                return new ValveGameNewsResult { Error = "Could not parse any news links from Steam." };
            }

            return new ValveGameNewsResult { Items = items };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ValveGameNewsResult
            {
                Error = ex.Message is { Length: > 0 } m
                    ? $"Could not load Steam news: {m}"
                    : "Could not load Steam news.",
            };
        }
    }

    /// <summary>Valve’s news body: lines separated by <c>\\</c> or newlines. Preserve paragraphs; strip tags.</summary>
    private static string ToPostBody(string? contents, string fallback, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(contents)) return (fallback ?? "").Trim();
        var segs = contents
            .Split(new[] { '\\', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>(segs.Length);
        foreach (var s in segs)
        {
            var t = TagStrip.Replace(s, " ");
            t = t.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
                .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
                .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
                .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
                .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase);
            t = Regex.Replace(t, @"[ \t]+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
            if (t.Length > 0) parts.Add(t);
        }

        if (parts.Count == 0) return (fallback ?? "").Trim();
        var body = string.Join("\n\n", parts);
        if (body.Length > maxLen) body = string.Concat(body.AsSpan(0, maxLen - 1), "…");
        return body;
    }

    private sealed class SteamNewsEnvelope
    {
        [JsonPropertyName("appnews")]
        public AppNews? Appnews { get; set; }
    }

    private sealed class AppNews
    {
        [JsonPropertyName("newsitems")]
        public List<NewsItem>? Newsitems { get; set; }
    }

    private sealed class NewsItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("contents")]
        public string? Contents { get; set; }

        [JsonPropertyName("feedname")]
        public string? Feedname { get; set; }
    }
}
