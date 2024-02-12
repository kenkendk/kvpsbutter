namespace KVPSButter;

/// <summary>
/// The core KVPS interface, describing operations on a key-value-pair store
/// </summary>
public interface IKVPS : IDisposable
{
    /// <summary>
    /// Attempts to get the information about an object at the given <paramref name="key"/>
    /// </summary>
    /// <param name="key">The key to query</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The entry information, or <c>null</c> if there is no entry at the path</returns>
    Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the content stream from the <paramref name="key"/>
    /// </summary>
    /// <param name="key">The key to read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The stream with the current content or <c>null</c> if there is no entry</returns>
    Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites or creates the entry at <paramref name="key"/> with <paramref name="data"/>
    /// </summary>
    /// <param name="key">The key to write</param>
    /// <param name="data">The data to write</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a the entry at <paramref name="key"/> if it exists
    /// </summary>
    /// <param name="key">The path to the file</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all entries with an optional query filter
    /// </summary>
    /// <param name="query">The optional query to use</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The entries matching the path and query</returns>
    IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, CancellationToken cancellationToken = default);
}
