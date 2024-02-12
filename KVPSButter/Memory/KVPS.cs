using System.Runtime.CompilerServices;

namespace KVPSButter.Memory;

/// <summary>
/// Implements <see cref="IKVPS"/> with an in-memory instance
/// </summary>
public class KVPS : IKVPS
{
    /// <summary>
    /// The storage entry
    /// </summary>
    private readonly Dictionary<string, byte[]> m_storage = new();

    /// <inheritdoc/>
    public async Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await data.CopyToAsync(ms, cancellationToken);
        m_storage[key] = ms.ToArray();
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        m_storage.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        query ??= KVPSQuery.Empty;

        var count = 0;

        foreach (var p in m_storage.Where(x => query.Matches(x.Key)).Select(x => GetInfo(x.Key)).Where(x => x != null))
        {
            yield return p!;
            if (query.MaxResults.HasValue && ++count > query.MaxResults.Value)
                yield break;

        }
    }

    /// <summary>
    /// Returns the info for the given <paramref name="key"/>
    /// </summary>
    /// <param name="key">The key to find info for</param>
    /// <returns>The entry information or <c>null</c></returns>
    private KVP? GetInfo(string key)
    {
        if (!m_storage.TryGetValue(key, out var data))
            return null;

        return new KVP(key, data.Length, null, null, null, null, null);
    }

    /// <inheritdoc/>
    public Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetInfo(key));

    /// <inheritdoc/>
    public Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!m_storage.TryGetValue(key, out var data))
            return Task.FromResult((Stream?)null);

        return Task.FromResult<Stream?>(new MemoryStream(data));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
