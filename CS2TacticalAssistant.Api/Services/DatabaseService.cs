using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// --- MySQL database connection ---
/// Prefer Railway’s single <c>mysql://…</c> URL (<c>MYSQL_URL</c> internal, <c>MYSQL_PUBLIC_URL</c> public) so user/host/password stay in sync.
/// Otherwise set <c>MYSQL_HOST</c>, <c>MYSQL_PORT</c>, <c>MYSQL_USER</c>, <c>MYSQL_PASSWORD</c>, <c>MYSQL_DATABASE</c> (or Railway’s <c>MYSQLHOST</c>, …).
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    public string ConnectionString { get; }

    public DatabaseService()
    {
        var sslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE")?.Trim();
        if (string.IsNullOrEmpty(sslMode)) sslMode = "Preferred";

        // If `mysql://` URL auth fails (stale ref, or ':' inside password confusing URI parsing), set MYSQL_PREFER_SPLIT_ENV=1
        // on the web service and use MYSQLHOST / MYSQLPASSWORD / … references instead.
        var preferSplit = IsTruthy(Environment.GetEnvironmentVariable("MYSQL_PREFER_SPLIT_ENV"));
        var mysqlUrl = preferSplit
            ? null
            : FirstNonEmpty(
                Environment.GetEnvironmentVariable("MYSQL_URL"),
                Environment.GetEnvironmentVariable("MYSQL_PUBLIC_URL"));

        if (!string.IsNullOrWhiteSpace(mysqlUrl) && mysqlUrl.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionString = BuildConnectionStringFromMysqlUrl(mysqlUrl, sslMode);
            return;
        }

        var host = EnvAny("MYSQL_HOST", "MYSQLHOST");
        var port = EnvAny("MYSQL_PORT", "MYSQLPORT");
        var user = EnvAny("MYSQL_USER", "MYSQLUSER");
        var password = EnvAny("MYSQL_PASSWORD", "MYSQLPASSWORD");
        var database = EnvAny("MYSQL_DATABASE", "MYSQLDATABASE");

        ConnectionString =
            $"Server={host};Port={port};User ID={user};Password={password};Database={database};"
            + $"SslMode={sslMode};AllowPublicKeyRetrieval=true;Maximum Pool Size=20;";
    }

    public MySqlConnection CreateConnection() => new(ConnectionString);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }

        return null;
    }

    private static string BuildConnectionStringFromMysqlUrl(string rawUrl, string sslMode)
    {
        // Uri does not accept mysql:// — swap to http:// for parsing only.
        var uri = new Uri(
            rawUrl.Replace("mysql://", "http://", StringComparison.OrdinalIgnoreCase),
            UriKind.Absolute);

        // user:password — password may contain ':' if encoded; unencoded ':' breaks first-split; take first segment as user, rest as password.
        var userInfo = uri.UserInfo;
        var colon = userInfo.IndexOf(':');
        var user = colon >= 0 ? Uri.UnescapeDataString(userInfo[..colon]) : Uri.UnescapeDataString(userInfo);
        var password = colon >= 0 ? Uri.UnescapeDataString(userInfo[(colon + 1)..]) : "";

        var host = uri.IdnHost;
        var port = uri.Port > 0 ? uri.Port : 3306;
        var database = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(database))
            throw new InvalidOperationException("MYSQL_URL / MYSQL_PUBLIC_URL is missing host or database path.");

        return
            $"Server={host};Port={port};User ID={user};Password={password};Database={database};"
            + $"SslMode={sslMode};AllowPublicKeyRetrieval=true;Maximum Pool Size=20;";
    }

    private static bool IsTruthy(string? v) =>
        string.Equals(v?.Trim(), "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(v?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);

    private static string EnvAny(string primaryKey, string fallbackKey)
    {
        var v = Environment.GetEnvironmentVariable(primaryKey);
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        v = Environment.GetEnvironmentVariable(fallbackKey);
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        throw new InvalidOperationException(
            $"Missing MySQL setting: set MYSQL_URL (or MYSQL_PUBLIC_URL), or '{primaryKey}' / '{fallbackKey}'. See example.env in repo root.");
    }
}
