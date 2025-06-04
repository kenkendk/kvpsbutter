using System.ComponentModel;
using Npgsql;

namespace KVPSButter.Postgres;

/// <summary>
/// Loader factory for PostgreSQL based KVPS implementation
/// </summary>
public class Loader : IKVPSFactory
{
    /// <summary>
    /// The default schemes suggested by the loader
    /// </summary>
    public IEnumerable<string> SupportedSchemes
        => ["postgres", "postgresql", "pgsql"];

    /// <inheritdoc/>
    public IEnumerable<Option> SupportedOptions
        => ParsedConnectionString.GetSupportedOptions<Config>();

    /// <inheritdoc/>
    public string Description => "A PostgreSQL-compatible storage provider";

    /// <inheritdoc/>
    public string? UsageInstructions => @"Use the PostgreSQL connection format:
    postgres://?username=username&password=password&port=port&host=host&tablename=mytable
    
    The table should have the following structure:
    CREATE TABLE mytable (
        keyname TEXT PRIMARY KEY,
        keyvalue BYTEA NOT NULL,
        size BIGINT NOT NULL,
        last_modified TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
    );";

    private record Config(
        [Description("The database username")]
        string Username,
        [Description("The database password")]
        string Password,
        [Description("The database host")]
        string Host,
        [Description("The database port")]
        int Port = 5432,
        [Description("The database name")]
        string Database = "",
        [Description("The table name to use for key-value storage")]
        string TableName = "kvps",
        [Description("Additional connection string options")]
        string? ConnectionOptions = null,
        [Description("The connection timeout in seconds")]
        int CommandTimeout = 30
    );

    /// <inheritdoc/>
    public IKVPS Create(string connectionString)
    {
        var (parsed, config) = KVPSLoader.ParseConnectionString<Config>(connectionString);

        var host = config.Host;
        var port = config.Port;
        var database = config.Database;

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOptionException("The database host is required");

        if (string.IsNullOrWhiteSpace(database))
            throw new InvalidOptionException("The database name is required");

        if (string.IsNullOrWhiteSpace(config.TableName))
            throw new InvalidOptionException("The table name is required");

        var connStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = config.Username,
            Password = config.Password,
            CommandTimeout = config.CommandTimeout
        };

        if (!string.IsNullOrWhiteSpace(config.ConnectionOptions)) connStringBuilder.ConnectionString += ";" + config.ConnectionOptions;

        return new KVPS(connStringBuilder.ConnectionString, config.TableName);
    }
}