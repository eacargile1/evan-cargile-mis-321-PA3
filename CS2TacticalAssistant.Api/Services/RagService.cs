using CS2TacticalAssistant.Api.Models;
using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// --- RAG retrieval ---
/// Retrieves Counter-Strike knowledge chunks from MySQL for injection into the LLM prompt.
/// Uses FULLTEXT when possible, then LIKE fallback for short tokens / stopword-only queries.
/// </summary>
public sealed class RagService(IDatabaseService db) : IRagService
{
    public async Task<IReadOnlyList<KnowledgeChunkDto>> SearchAsync(string query, int take = 8, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length == 0) return Array.Empty<KnowledgeChunkDto>();

        await using var conn = db.CreateConnection();
        await conn.OpenAsync(ct);

        var results = new List<KnowledgeChunkDto>();

        // Try FULLTEXT first (MySQL ignores very short words by default)
        const string ftSql = """
            SELECT id, category, title, content, tags,
                   MATCH(title, content) AGAINST (@q IN NATURAL LANGUAGE MODE) AS score
            FROM knowledge_chunks
            WHERE MATCH(title, content) AGAINST (@q IN NATURAL LANGUAGE MODE)
            ORDER BY score DESC
            LIMIT @take
            """;

        await using (var cmd = new MySqlCommand(ftSql, conn))
        {
            cmd.Parameters.AddWithValue("@q", query);
            cmd.Parameters.AddWithValue("@take", take);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(ReadChunk(reader));
            }
        }

        if (results.Count > 0) return results;

        // LIKE fallback for grading queries that FULLTEXT may skip
        var term = query.Length > 64 ? query[..64] : query;
        const string likeSql = """
            SELECT id, category, title, content, tags, 0 AS score
            FROM knowledge_chunks
            WHERE LOWER(title) LIKE LOWER(CONCAT('%', @t, '%'))
               OR LOWER(content) LIKE LOWER(CONCAT('%', @t, '%'))
               OR LOWER(tags) LIKE LOWER(CONCAT('%', @t, '%'))
            ORDER BY id DESC
            LIMIT @take
            """;

        await using (var cmd2 = new MySqlCommand(likeSql, conn))
        {
            cmd2.Parameters.AddWithValue("@t", term);
            cmd2.Parameters.AddWithValue("@take", take);
            await using var reader = await cmd2.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(ReadChunk(reader));
            }
        }

        return results;
    }

    private static KnowledgeChunkDto ReadChunk(MySqlDataReader reader)
    {
        var scoreOrd = reader.GetOrdinal("score");
        double? score = reader.IsDBNull(scoreOrd) ? null : reader.GetDouble(scoreOrd);
        return new KnowledgeChunkDto(
            reader.GetUInt64("id"),
            reader.GetString("category"),
            reader.GetString("title"),
            reader.GetString("content"),
            reader.IsDBNull(reader.GetOrdinal("tags")) ? null : reader.GetString("tags"),
            score);
    }
}
