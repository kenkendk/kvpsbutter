
namespace KVPSButter;

/// <summary>
/// The IKVPS interface extended with JSON methods
/// </summary>
public interface IKVPSJson : IKVPS
{
    /// <summary>
    /// Returns the content from the <paramref name="key"/>, deserialized as <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="key">The key to read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The stream with the current content or <c>null</c> if there is no entry</returns>
    Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites or creates the entry at <paramref name="key"/> with a serialized version of <paramref name="data"/>
    /// </summary>
    /// <param name="key">The key to write</param>
    /// <param name="data">The data to write</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    Task WriteJsonAsync<T>(string key, T? data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extender for supporting <see cref="IKVPSJson"/> on a <see cref="IKVPS"/> that has no native JSON support
/// </summary>
public class KVPSJsonExtender : KVPSExtender, IKVPSJson
{
    public KVPSJsonExtender(IKVPS parent)
        : base(parent)
    { }

    /// <inheritdoc/>
    public Task WriteJsonAsync<T>(string key, T? data, CancellationToken cancellationToken)
        => WriteAsync(key, SerializeObject(data), cancellationToken);


    /// <inheritdoc/>
    public Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken)
        => DeserializeStreamAsync<T>(ReadAsync(key, cancellationToken));

    /// <summary>
    /// Helper method to deserialize remote stream as JSON if there is no native support for it
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="stream">The stream to parse</param>
    /// <returns>The deserialized object</returns>
    public static async Task<T?> DeserializeStreamAsync<T>(Stream? stream)
    {
        if (stream == null)
            return await Task.FromResult<T?>(default(T?));

        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream);
    }

    /// <summary>
    /// Helper method to deserialize remote stream as JSON if there is no native support for it
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="stream">The stream to parse</param>
    /// <returns>The deserialized object</returns>
    public static async Task<T?> DeserializeStreamAsync<T>(Task<Stream?> stream)
        => await DeserializeStreamAsync<T>(await stream);


    /// <summary>
    /// Helper method to serialize object into remote stream as JSON if there is no native support for it
    /// </summary>
    /// <param name="data">The object to serialize</param>
    /// <returns>The deserialized data</returns>
    public static Stream SerializeObject(object? data)
    {
        if (data == null)
            return new MemoryStream();
        return new MemoryStream(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data));
    }

    /// <summary>
    /// Wraps an <see cref="IKVPS"/> into a <see cref="IKVPSJson"/>
    /// </summary>
    /// <param name="store">The store to extend</param>
    /// <returns>The wrapped instance</returns>
    public static IKVPSJson ExtendIKVPS(IKVPS store)
    {
        // If the store already supports IKVSPJson, just return it
        if (store is IKVPSJson kvpsj)
            return kvpsj;

        return new KVPSJsonExtender(store);
    }
}

/// <summary>
/// Extension methods for making it transparent if an <see cref="KVPSButter"> instance has native JSON support
/// </summary>
public static class IKVPSJsonExtensions
{
    /// <summary>
    /// Writes item as serialized Json
    /// </summary>
    /// <typeparam name="T">The type to serialize</typeparam>
    /// <param name="kvps">The <see cref="IKVPS"/> instance</param>
    /// <param name="key">The key to write</param>
    /// <param name="data">The data to serialize</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    public static Task WriteJsonAsync<T>(this IKVPS kvps, string key, T? data, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSJson kvpsj)
            return kvpsj.WriteJsonAsync<T>(key, data, cancellationToken);

        return kvps.WriteAsync(key, KVPSJsonExtender.SerializeObject(data), cancellationToken);
    }

    /// <summary>
    /// Reads a deserializes a stream as Json
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="kvps">The <see cref="IKVPS"/> instance</param>
    /// <param name="key">The key to read</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The deserialized item</returns>
    public static Task<T?> ReadJsonAsync<T>(this IKVPS kvps, string key, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSJson kvpsj)
            return kvpsj.ReadJsonAsync<T>(key, cancellationToken);

        return KVPSJsonExtender.DeserializeStreamAsync<T>(kvps.ReadAsync(key, cancellationToken));
    }

}