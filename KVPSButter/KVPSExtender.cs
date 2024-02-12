namespace KVPSButter;

/// <summary>
/// Abstract extender class for sub-classing an <see cref="IKVPS"/>
/// </summary>
public abstract class KVPSExtender
{
    /// <summary>
    /// The <see cref="IKVPS"/> parent
    /// </summary>
    protected readonly IKVPS m_parent;

    /// <summary>
    /// Creates a new extender for the parent store
    /// </summary>
    /// <param name="parent">The parent store</param>
    public KVPSExtender(IKVPS parent)
        => m_parent = parent;

    /// <inheritdoc/>
    public Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
        => m_parent.WriteAsync(key, data, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => m_parent.DeleteAsync(key, cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
        => m_parent.Dispose();

    /// <inheritdoc/>
    public IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, CancellationToken cancellationToken = default)
        => m_parent.EnumerateAsync(query, cancellationToken);

    /// <inheritdoc/>
    public Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
        => m_parent.GetInfoAsync(key, cancellationToken);

    /// <inheritdoc/>
    public Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
        => m_parent.ReadAsync(key, cancellationToken);
}
