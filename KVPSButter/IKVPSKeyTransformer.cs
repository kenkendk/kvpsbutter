
using System.Runtime.CompilerServices;

namespace KVPSButter;

/// <summary>
/// Interface for mapping a local key to a remove and vice-versa
/// </summary>
public interface IKVPSKeyTransformer
{
    /// <summary>
    /// The function that maps a local (user) key to the remote (storage) key
    /// </summary>
    /// <param name="localKey">The local key to map</param>
    /// <returns>The remote key</returns>
    string LocalKeyToRemoteKey(string localKey);
    /// <summary>
    /// The function that maps a remote (storage) key to a local (user) key
    /// </summary>
    /// <param name="remoteKey">The remote key to map</param>
    /// <returns>The local key</returns>
    string RemoteKeyToLocalKey(string remoteKey);
}

/// <summary>
/// Class that adds a prefix to keys
/// </summary>
public class PrefixKeyTransformer : IKVPSKeyTransformer
{
    /// <summary>
    /// The prefix to add to a local key
    /// </summary>
    private string m_prefix;

    /// <summary>
    /// Constructs a new key transformer with the given prefix
    /// </summary>
    /// <param name="prefix"></param>
    public PrefixKeyTransformer(string prefix)
    {
        m_prefix = prefix ?? string.Empty;
    }

    public string LocalKeyToRemoteKey(string localKey)
    {
        return m_prefix + localKey;
    }

    public string RemoteKeyToLocalKey(string remoteKey)
    {
        if (!remoteKey.StartsWith(m_prefix))
            throw new InvalidKeyException("Remote key did not start wit the expected prefix");
        return remoteKey.Substring(m_prefix.Length);
    }
}

public class KVPSKeyWrapper : IKVPS
{
    protected readonly IKVPS m_parent;
    protected readonly IKVPSKeyTransformer m_keyTransformer;

    public KVPSKeyWrapper(IKVPS parent, IKVPSKeyTransformer keyTransformer)
    {
        m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
        m_keyTransformer = keyTransformer ?? throw new ArgumentNullException(nameof(keyTransformer));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => m_parent.DeleteAsync(m_keyTransformer.LocalKeyToRemoteKey(key), cancellationToken);

    public void Dispose()
    {
        m_parent.Dispose();
    }

    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var q = query ?? KVPSQuery.Empty;
        if (q.Prefix != null)
            q = q with { Prefix = m_keyTransformer.LocalKeyToRemoteKey(q.Prefix) };
        await foreach (var k in m_parent.EnumerateAsync(q, cancellationToken))
            yield return k with { Key = m_keyTransformer.RemoteKeyToLocalKey(k.Key) };
    }

    public async Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
    {
        var t = await m_parent.GetInfoAsync(m_keyTransformer.LocalKeyToRemoteKey(key), cancellationToken);
        if (t != null)
            t = t with { Key = m_keyTransformer.RemoteKeyToLocalKey(key) };
        return t;
    }

    public Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
        => m_parent.ReadAsync(m_keyTransformer.LocalKeyToRemoteKey(key), cancellationToken);

    public Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
        => m_parent.WriteAsync(m_keyTransformer.LocalKeyToRemoteKey(key), data, cancellationToken);
}

public class KVPSKeyWrapperBatch : KVPSKeyWrapper, IKVPSBatch
{
    protected readonly IKVPSBatch m_parentBatched;

    public KVPSKeyWrapperBatch(IKVPSBatch parent, IKVPSKeyTransformer keyTransformer)
        : base(parent, keyTransformer)
    {
        m_parentBatched = parent;
    }

    public Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => m_parentBatched.DeleteAsync(keys.Select(x => m_keyTransformer.LocalKeyToRemoteKey(x)), cancellationToken);

    public async IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IEnumerable<string> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var (k, e) in m_parentBatched.GetInfoAsync(keys.Select(x => m_keyTransformer.LocalKeyToRemoteKey(x)), cancellationToken))
        {
            var re = e;
            if (re != null)
                re = re with { Key = m_keyTransformer.RemoteKeyToLocalKey(re.Key) };
            yield return (m_keyTransformer.RemoteKeyToLocalKey(k), re);
        }
    }

    public async IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IEnumerable<string> keys, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var (k, e) in m_parentBatched.ReadAsync(keys.Select(x => m_keyTransformer.LocalKeyToRemoteKey(x)), cancellationToken))
        {
            yield return (m_keyTransformer.RemoteKeyToLocalKey(k), e);
        }
    }

    public Task WriteAsync(IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
        => m_parentBatched.WriteAsync(values.Select(x => (m_keyTransformer.LocalKeyToRemoteKey(x.Key), x.Stream)), cancellationToken);
}

public static class KVPSKeyTransformerExtensions
{
    public static IKVPS WrapKeys(this IKVPS kvps, IKVPSKeyTransformer keyTransformer)
    {
        if (kvps is IKVPSBatch kvpsb)
            return new KVPSKeyWrapperBatch(kvpsb, keyTransformer);
        return new KVPSKeyWrapper(kvps, keyTransformer);
    }
}