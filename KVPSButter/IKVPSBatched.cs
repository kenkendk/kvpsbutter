using System.Runtime.CompilerServices;

namespace KVPSButter;

/// <summary>
/// Interface for batch-style operations on <see cref="IKVPS"/>
/// </summary>
public interface IKVPSBatch : IKVPS
{
    /// <summary>
    /// Returns a set of information entries
    /// </summary>
    /// <param name="keys">The keys to query</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The list of info entries</returns>
    IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IEnumerable<string> keys, CancellationToken cancellationToken);

    /// <summary>
    /// Reads all keys and returns the contents
    /// </summary>
    /// <param name="keys">The keys to return</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The streams and keys</returns>
    IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IEnumerable<string> keys, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a set of key-value pairs
    /// </summary>
    /// <param name="values">The values to write</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    Task WriteAsync(IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the values associated with the keys
    /// </summary>
    /// <param name="keys">The keys to delete</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken);
}

/// <summary>
/// Extender class for giving <see cref="IKVPSBatch"/> functionality on a <see cref="IKVPS"/> without native support
/// </summary>
public class KVPSBatchExtender : KVPSExtender, IKVPSBatch
{
    /// <summary>
    /// Creates a new instance of <see cref="KVPSBatchExtender"/>
    /// </summary>
    /// <param name="parent">The <see cref="IKVPS"/> parent</param>
    public KVPSBatchExtender(IKVPS parent)
        : base(parent)
    { }

    /// <inheritdoc/>
    public Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => DeleteAsync(m_parent, keys, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => GetInfoAsync(m_parent, keys, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => ReadAsync(m_parent, keys, cancellationToken);

    /// <inheritdoc/>
    public Task WriteAsync(IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
        => WriteAsync(m_parent, values, cancellationToken);

    /// <summary>
    /// Deletes the values associated with the keys
    /// </summary>
    /// <param name="parent">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to delete</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    public static async Task DeleteAsync(IKVPS parent, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await parent.DeleteAsync(key, cancellationToken);
        }
    }

    /// <summary>
    /// Returns a set of information entries
    /// </summary>
    /// <param name="parent">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to query</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The list of info entries</returns>
    public static async IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IKVPS parent, IEnumerable<string> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (key, await parent.GetInfoAsync(key, cancellationToken));
        }
    }

    /// <summary>
    /// Reads all keys and returns the contents
    /// </summary>
    /// <param name="parent">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to return</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The streams and keys</returns>
    public static async IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IKVPS parent, IEnumerable<string> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (key, await parent.ReadAsync(key, cancellationToken));
        }
    }

    /// <summary>
    /// Writes a set of key-value pairs
    /// </summary>
    /// <param name="parent">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="values">The values to write</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    public static async Task WriteAsync(IKVPS parent, IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
    {
        foreach (var (key, stream) in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await parent.WriteAsync(key, stream, cancellationToken);
        }
    }

    /// <summary>
    /// Wraps an <see cref="IKVPS"/> into a <see cref="IKVPSBatch"/>
    /// </summary>
    /// <param name="store">The store to extend</param>
    /// <returns>The wrapped instance</returns>
    public static IKVPSBatch ExtendIKVPS(IKVPS store)
    {
        // If the store already supports IKVPSBatch, just return it
        if (store is IKVPSBatch kvpsb)
            return kvpsb;

        return new KVPSBatchExtender(store);
    }
}

/// <summary>
/// Extension methods for making it transparent if an <see cref="IKVPS"> instance has native batch support
/// </summary>
public static class IKVPSBatchExtensions
{
    /// <summary>
    /// Returns a set of information entries
    /// </summary>
    /// <param name="kvps">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to query</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The list of info entries</returns>
    public static IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(this IKVPS kvps, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSBatch ikvpsb)
            return ikvpsb.GetInfoAsync(keys, cancellationToken);

        return KVPSBatchExtender.GetInfoAsync(kvps, keys, cancellationToken);
    }

    /// <summary>
    /// Deletes the values associated with the keys
    /// </summary>
    /// <param name="kvps">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to delete</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    public static Task DeleteAsync(this IKVPS kvps, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSBatch ikvpsb)
            return ikvpsb.DeleteAsync(keys, cancellationToken);

        return KVPSBatchExtender.DeleteAsync(kvps, keys, cancellationToken);
    }

    /// <summary>
    /// Reads all keys and returns the contents
    /// </summary>
    /// <param name="kvps">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="keys">The keys to return</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>The streams and keys</returns>
    public static IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(this IKVPS kvps, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSBatch ikvpsb)
            return ikvpsb.ReadAsync(keys, cancellationToken);

        return KVPSBatchExtender.ReadAsync(kvps, keys, cancellationToken);
    }

    /// <summary>
    /// Writes a set of key-value pairs
    /// </summary>
    /// <param name="kvps">The <see cref="IKVPS"/> store to wrap</param>
    /// <param name="values">The values to write</param>
    /// <param name="cancellationToken">The optional cancellation token</param>
    /// <returns>An awaitable task</returns>
    public static Task WriteAsync(this IKVPS kvps, IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
    {
        if (kvps is IKVPSBatch ikvpsb)
            return ikvpsb.WriteAsync(values, cancellationToken);

        return KVPSBatchExtender.WriteAsync(kvps, values, cancellationToken);
    }
}


