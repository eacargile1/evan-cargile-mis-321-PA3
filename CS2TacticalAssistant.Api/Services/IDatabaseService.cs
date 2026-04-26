using MySqlConnector;

namespace CS2TacticalAssistant.Api.Services;

/// <summary>
/// MySQL database access. Connection string is built from MYSQL_* environment variables
/// (Heroku JawsDB / ClearDB style) — see DatabaseService; never commit secrets.
/// </summary>
public interface IDatabaseService
{
    string ConnectionString { get; }
    MySqlConnection CreateConnection();
}
