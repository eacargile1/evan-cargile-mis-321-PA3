using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// --- MySQL database connection ---
/// Set MYSQL_HOST, MYSQL_PORT, MYSQL_USER, MYSQL_PASSWORD, MYSQL_DATABASE (see example.env).
/// Railway’s MySQL plugin also injects MYSQLHOST, MYSQLPORT, MYSQLUSER, MYSQLPASSWORD, MYSQLDATABASE — those are accepted as fallbacks.
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    public string ConnectionString { get; }

    public DatabaseService()
    {
        var host = EnvAny("MYSQL_HOST", "MYSQLHOST");
        var port = EnvAny("MYSQL_PORT", "MYSQLPORT");
        var user = EnvAny("MYSQL_USER", "MYSQLUSER");
        var password = EnvAny("MYSQL_PASSWORD", "MYSQLPASSWORD");
        var database = EnvAny("MYSQL_DATABASE", "MYSQLDATABASE");
        // Cloud MySQL (Railway, Azure, etc.) often needs Required or VerifyFull — set MYSQL_SSL_MODE if the default fails.
        var sslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE")?.Trim();
        if (string.IsNullOrEmpty(sslMode)) sslMode = "Preferred";

        ConnectionString =
            $"Server={host};Port={port};User ID={user};Password={password};Database={database};"
            + $"SslMode={sslMode};AllowPublicKeyRetrieval=true;Maximum Pool Size=20;";
    }

    public MySqlConnection CreateConnection() => new(ConnectionString);

    private static string EnvAny(string primaryKey, string fallbackKey)
    {
        var v = Environment.GetEnvironmentVariable(primaryKey);
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        v = Environment.GetEnvironmentVariable(fallbackKey);
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        throw new InvalidOperationException(
            $"Missing MySQL setting: set '{primaryKey}' or (Railway) '{fallbackKey}'. See example.env in repo root.");
    }
}
