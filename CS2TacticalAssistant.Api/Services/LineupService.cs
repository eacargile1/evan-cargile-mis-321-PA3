using CS2TacticalAssistant.Api.Models;
using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

public sealed class LineupService(IDatabaseService db) : ILineupService
{
    public async Task<IReadOnlyList<LineupDto>> SearchAsync(string map, string? site, string? grenadeType, string? side, CancellationToken ct = default)
    {
        await using var conn = db.CreateConnection();
        await conn.OpenAsync(ct);

        var sql = """
            SELECT id, lineup_name, purpose, instructions, when_to_use, map_name, site, grenade_type, side
            FROM lineup_library
            WHERE LOWER(map_name) = LOWER(@map)
              AND (@site IS NULL OR LOWER(RTRIM(site)) = LOWER(RTRIM(@site)))
              AND (@gt IS NULL OR LOWER(RTRIM(grenade_type)) = LOWER(RTRIM(@gt)))
              AND (@side IS NULL OR RTRIM(side) = RTRIM(@side))
            ORDER BY id ASC
            """;

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@map", map.Trim());
        cmd.Parameters.AddWithValue("@site", string.IsNullOrWhiteSpace(site) ? (object)DBNull.Value : site.Trim());
        cmd.Parameters.AddWithValue("@gt", string.IsNullOrWhiteSpace(grenadeType) ? (object)DBNull.Value : grenadeType.Trim());
        cmd.Parameters.AddWithValue("@side", string.IsNullOrWhiteSpace(side) ? (object)DBNull.Value : side.Trim().ToUpperInvariant());

        var list = new List<LineupDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new LineupDto
            {
                Id = reader.GetUInt64("id"),
                LineupName = reader.GetString("lineup_name"),
                Purpose = reader.GetString("purpose"),
                Instructions = reader.GetString("instructions"),
                WhenToUse = reader.GetString("when_to_use"),
                Map = reader.GetString("map_name"),
                Site = reader.GetString("site"),
                GrenadeType = reader.GetString("grenade_type"),
                Side = reader.GetString("side")
            });
        }

        return list;
    }
}
