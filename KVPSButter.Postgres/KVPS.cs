using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;

namespace KVPSButter.Postgres;

/// <summary>
/// Implementation of the <see cref="IKVPS"/> interface for PostgreSQL storage
/// </summary>
public class KVPS : IKVPSBatch
{
    /// <summary>
    /// The PostgreSQL connectionstring
    /// </summary>
    private readonly string _connectionString;
    /// <summary>
    /// The table name to use for storage
    /// </summary>
    private readonly string _tableName;
    /// <summary>
    /// The cursor prefix for pagination
    /// </summary>
    private const string CursorPrefix = "pg1";

    /// <summary>
    /// Creates a new KVPS instance
    /// </summary>
    /// <param name="connectionString">The connectionString for the database</param>
    /// <param name="tableName">The table name to use for storage</param>
    public KVPS(string connectionString, string tableName)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentNullException(nameof(tableName));
        _tableName = tableName;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var cmd = CreateCommandAsync($"DELETE FROM {_tableName} WHERE keyname = @key", connection);
        cmd.Parameters.AddWithValue("key", key);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {

    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        query ??= KVPSQuery.Empty;

        var remaining = query.MaxResults ?? int.MaxValue;
        var pageSize = query.PageSize ?? 1000;
        var offset = 0;

        var whereClause = string.IsNullOrWhiteSpace(query.Prefix)
            ? ""
            : "WHERE keyname LIKE @prefix";

        var sql = $@"
            SELECT keyname, size, last_modified 
            FROM {_tableName} 
            {whereClause}
            ORDER BY keyname 
            LIMIT @limit OFFSET @offset";

        while (remaining > 0)
        {
            await using var connection = await CreateConnectionAsync(cancellationToken);
            await using var cmd = CreateCommandAsync(sql, connection);
            cmd.Parameters.AddWithValue("limit", Math.Min(remaining, pageSize));
            cmd.Parameters.AddWithValue("offset", offset);

            if (!string.IsNullOrWhiteSpace(query.Prefix))
                cmd.Parameters.AddWithValue("prefix", query.Prefix + "%");

            var count = 0;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                count++;
                remaining--;
                offset++;

                var key = reader.GetString(0);
                var size = reader.GetInt64(1);
                var lastModified = reader.GetDateTime(2);

                yield return new KVP(
                    key,
                    size,
                    null,
                    lastModified,
                    $"{CursorPrefix}:{offset}",
                    null,
                    null
                );

                if (remaining <= 0)
                    yield break;
            }

            // If we got fewer results than requested, we've reached the end
            if (count < Math.Min(remaining + count, pageSize))
                yield break;
        }
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private NpgsqlCommand CreateCommandAsync(string sql, NpgsqlConnection connection)
    {
        return new NpgsqlCommand(sql, connection);
    }

    /// <inheritdoc/>
    public async Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var cmd = CreateCommandAsync($"SELECT size, last_modified FROM {_tableName} WHERE keyname = @key", connection);
        cmd.Parameters.AddWithValue("key", key);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var size = reader.GetInt64(0);
            var lastModified = reader.GetDateTime(1);

            return new KVP(
                key,
                size,
                null, // Created not tracked in basic schema
                lastModified,
                null, // No cursor for single item
                null, // ETag not implemented for PostgreSQL
                null
            );
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var cmd = CreateCommandAsync($"SELECT keyvalue FROM {_tableName} WHERE keyname = @key", connection);
        cmd.Parameters.AddWithValue("key", key);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is byte[] data)
            return new MemoryStream(data);

        return null;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await data.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        var sql = $@"
        INSERT INTO {_tableName} (keyname, keyvalue, size, last_modified) 
        VALUES (@keyname, @keyvalue, @size, @lastmodified)
        ON CONFLICT (keyname) 
            DO UPDATE SET 
        keyvalue = EXCLUDED.keyvalue, 
        size = EXCLUDED.size, 
        last_modified = EXCLUDED.last_modified";

        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var cmd = CreateCommandAsync(sql, connection);
        cmd.Parameters.Add("keyname", NpgsqlDbType.Text).Value = key;
        cmd.Parameters.Add("keyvalue", NpgsqlDbType.Bytea).Value = bytes;
        cmd.Parameters.Add("size", NpgsqlDbType.Bigint).Value = (long)bytes.Length;
        cmd.Parameters.Add("lastmodified", NpgsqlDbType.TimestampTz).Value = DateTime.UtcNow;

        await cmd.ExecuteNonQueryAsync(cancellationToken);

    }

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => KVPSBatchExtender.GetInfoAsync(this, keys, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => KVPSBatchExtender.ReadAsync(this, keys, cancellationToken);

    /// <inheritdoc/>
    public Task WriteAsync(IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
        => KVPSBatchExtender.WriteAsync(this, values, cancellationToken);

    /// <inheritdoc/>
    public async Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        var keyList = keys.ToList();
        if (!keyList.Any()) return;

        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var cmd = CreateCommandAsync($"DELETE FROM {_tableName} WHERE keyname = ANY(@keys)", connection);
        cmd.Parameters.AddWithValue("keys", keyList.ToArray());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}