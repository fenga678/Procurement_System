using System.Text.Json.Serialization;

namespace CanteenProcurement.Infrastructure.Configuration;

public sealed class DatabaseConfiguration
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "Sqlite";

    [JsonPropertyName("sqlite")]
    public SqliteConfiguration Sqlite { get; set; } = new();
}

public sealed class SqliteConfiguration
{
    [JsonPropertyName("databasePath")]
    public string DatabasePath { get; set; } = "data/canteen.db";
}
