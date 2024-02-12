
using System.Runtime.CompilerServices;

namespace KVPSButter.Filesystem;

/// <summary>
/// Implementation of <see cref="IKVPS"/> for a local filesystem
/// </summary>
internal class KVPS : IKVPS
{
    /// <summary>
    /// The path where the KVP begins
    /// </summary>
    private readonly string m_root;

    /// <summary>
    /// Characters that are not supported in the key
    /// </summary>
    private static readonly HashSet<char> InvalidPathChars
        = Path.GetInvalidPathChars().Append('\\').Append(':').ToHashSet();

    /// <summary>
    /// Constructs a VFS instance for an existing root folder
    /// </summary>
    /// <param name="rootpath">The root path to use</param>
    internal KVPS(string rootpath)
    {
        rootpath = Path.GetFullPath(rootpath);
        if (!rootpath.EndsWith(Path.DirectorySeparatorChar))
            rootpath += Path.DirectorySeparatorChar;
        m_root = rootpath;
    }

    /// <summary>
    /// Maps a key to a local path
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The local path</returns>
    private string MapKeyToLocalPath(string key)
    {
        if (key.Any(x => InvalidPathChars.Contains(x)))
            throw new InvalidKeyException("The key is not absolute");

        var localPath = key;
        if (Path.DirectorySeparatorChar != '/')
            localPath = localPath.Replace(Path.DirectorySeparatorChar, '/');

        localPath = m_root + localPath;
        return EnsureLocalPathIsBelowRoot(localPath);
    }

    /// <summary>
    /// Maps a local absolute path to key
    /// </summary>
    /// <param name="localPath">The path to map</param>
    /// <returns>The mapped key</returns>
    private string MapLocalPathToKey(string localPath)
    {
        localPath = EnsureLocalPathIsBelowRoot(localPath);
        var remotePath = localPath.Substring(m_root.Length);
        if (Path.DirectorySeparatorChar != '/')
            remotePath = remotePath.Replace(Path.DirectorySeparatorChar, '/');

        return remotePath;
    }

    /// <summary>
    /// Checks if the <paramref name="localPath"/> is below the root, and throws an exception if not
    /// </summary>
    /// <param name="localPath">The local path to check</param>
    /// <returns>The local path</returns>
    private string EnsureLocalPathIsBelowRoot(string localPath)
        => localPath.StartsWith(m_root)
            ? localPath
            : throw new InvalidKeyException("Local path was outside root");

    /// <summary>
    /// Returns a <see cref="VFSEntry"> for the local path, or <c>null</c> if it does not exist
    /// </summary>
    /// <param name="localPath">The local path to use</param>
    /// <returns>The <see cref="VFSEntry"/> or <c>null</c></returns>
    private KVP? GetInfoFromLocalPath(string localPath)
    {
        var fi = new FileInfo(localPath);
        return fi.Exists
            ? new KVP(MapLocalPathToKey(localPath), fi.Length, fi.CreationTimeUtc, fi.LastWriteTimeUtc, null, null, null)
            : null;
    }

    /// <inheritdoc/>
    public Task<KVP?> GetInfoAsync(string remotePath, CancellationToken cancellationToken)
        => Task.FromResult(GetInfoFromLocalPath(MapKeyToLocalPath(remotePath)));

    /// <inheritdoc/>
    public Task<Stream?> ReadAsync(string remotePath, CancellationToken cancellationToken)
    {
        var localPath = MapKeyToLocalPath(remotePath);
        if (!File.Exists(localPath))
            return Task.FromResult((Stream?)null);
        return Task.FromResult<Stream?>(File.OpenRead(MapKeyToLocalPath(remotePath)));
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string remotePath, Stream data, CancellationToken cancellationToken)
    {
        // Recursive create of folders, going back until we find one or the root
        var localPath = MapKeyToLocalPath(remotePath);
        var topdir = Path.GetDirectoryName(localPath);
        while (topdir != null && localPath.Length > m_root.Length && !Directory.Exists(topdir))
        {
            Directory.CreateDirectory(topdir);
            topdir = Path.GetDirectoryName(topdir);
        }

        using var fs = File.OpenWrite(localPath);
        fs.SetLength(0);
        await data.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        var localPath = MapKeyToLocalPath(remotePath);
        if (!File.Exists(localPath))
            return Task.CompletedTask;

        File.Delete(localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        query ??= KVPSQuery.Empty;

        var options = new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            RecurseSubdirectories = true
        };

        // Pretend to be async
        await Task.CompletedTask;

        var localPath = MapKeyToLocalPath(query.Prefix ?? string.Empty);

        // If the path is partial, e.g. /root/ex*, we need to go up one folder to match
        if (!Directory.Exists(localPath))
        {
            var prev = Path.GetDirectoryName(localPath);
            if (prev != null)
            {
                if (!prev.EndsWith(Path.DirectorySeparatorChar))
                    prev += Path.DirectorySeparatorChar;

                if (prev.Length >= m_root.Length)
                    localPath = prev;
            }
        }

        // If the key points to something that does not exist, return empty
        if (!Directory.Exists(localPath))
            yield break;

        var count = 0;
        foreach (var p in Directory.EnumerateFiles(localPath, "*", options).Where(p => query.Matches(MapLocalPathToKey(p))))
        {
            yield return GetInfoFromLocalPath(p)!;
            if (query.MaxResults.HasValue && ++count > query.MaxResults.Value)
                yield break;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
