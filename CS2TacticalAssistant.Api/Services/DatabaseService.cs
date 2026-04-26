using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// --- MySQL database connection ---
/// Hosted deployment (Heroku + JawsDB): set MYSQL_HOST, MYSQL_PORT, MYSQL_USER, MYSQL_PASSWORD, MYSQL_DATABASE.
/// Local: copy example.env values or export the same variables before `dotnet run`.
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    public string ConnectionString { get; }

    public DatabaseService()
    {
        var host = Env("MYSQL_HOST");
        var port = Env("MYSQL_PORT");
        var user = Env("MYSQL_USER");
        var password = Env("MYSQL_PASSWORD");
        var database = Env("MYSQL_DATABASE");
        // Cloud MySQL (Railway, Azure, etc.) often needs Required or VerifyFull — set MYSQL_SSL_MODE if the default fails.
        var sslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE")?.Trim();
        if (string.IsNullOrEmpty(sslMode)) sslMode = "Preferred";

        ConnectionString =
            $"Server={host};Port={port};User ID={user};Password={password};Database={database};"
            + $"SslMode={sslMode};AllowPublicKeyRetrieval=true;Maximum Pool Size=20;";
    }

    public MySqlConnection CreateConnection() => new(ConnectionString);

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Missing required environment variable '{key}'. See example.env in repo root.");
}
