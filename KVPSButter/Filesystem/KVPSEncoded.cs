using System.Runtime.CompilerServices;
using System.Text;

namespace KVPSButter.Filesystem;

/// <summary>
/// Implementation of <see cref="IKVPS"/> with keys base64-encoded as filenames in a single directory
/// </summary>
public class KVPSEncoded : IKVPS
{
    /// <summary>
    /// The path where the KVP begins
    /// </summary>
    private readonly string m_directory;

    /// <summary>
    /// Constructs a VFS instance for an existing root folder
    /// </summary>
    /// <param name="directory">The directory to store entries in</param>
    internal KVPSEncoded(string directory)
    {
        directory = Path.GetFullPath(directory);
        if (!directory.EndsWith(Path.DirectorySeparatorChar))
            directory += Path.DirectorySeparatorChar;
        m_directory = directory;
    }

    /// <summary>
    /// Encodes a key to a path-safe entry
    /// </summary>
    /// <param name="key">The key to encode</param>
    /// <returns>The encoded key</returns>
    private static string EncodeKey(string key)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

    /// <summary>
    /// Decodes a path-safe entry to a key
    /// </summary>
    /// <param name="data">The path-safe entry</param>
    /// <returns>The key</returns>
    private static string DecodeKey(string data)
        => Encoding.UTF8.GetString(Convert.FromBase64String(data));

    /// <summary>
    /// Gets the local path for a key
    /// </summary>
    /// <param name="key">The key to get the local path for</param>
    /// <returns>The local path</returns>
    private string GetLocalPath(string key)
        => Path.Combine(m_directory, EncodeKey(key));

    /// <summary>
    /// Gets the key from a path
    /// </summary>
    /// <param name="localPath">The path to map to a key</param>
    /// <returns>The key</returns>
    private string GetKeyFromPath(string localPath)
    {
        if (!localPath.StartsWith(m_directory))
            throw new InvalidKeyException("Path is outside root");
        return DecodeKey(Path.GetFileName(localPath));
    }

    /// <summary>
    /// Returns the KVP for the key
    /// </summary>
    /// <param name="key">The key to find the information for</param>
    /// <returns>The KVP info</returns>
    private KVP? GetInfo(string key)
    {
        var localPath = GetLocalPath(key);
        var fi = new FileInfo(localPath);
        if (!fi.Exists)
            return null;

        return new KVP(key, fi.Length, fi.CreationTimeUtc, fi.LastWriteTimeUtc, null, null, null);
    }

    /// <inheritdoc/>
    public Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(GetInfo(key));

    /// <inheritdoc/>
    public Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var localPath = GetLocalPath(key);
        if (!File.Exists(localPath))
            return Task.FromResult((Stream?)null);
        return Task.FromResult<Stream?>(File.OpenRead(localPath));
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
    {
        var localPath = GetLocalPath(key);
        using var fs = File.OpenWrite(localPath);
        fs.SetLength(0);
        await data.CopyToAsync(fs, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var localPath = GetLocalPath(key);
        if (File.Exists(localPath))
            File.Delete(localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        query ??= KVPSQuery.Empty;
        var prefix = string.IsNullOrWhiteSpace(query.Prefix) ? string.Empty : EncodeKey(query.Prefix).TrimEnd('=')[..^1];
        await Task.CompletedTask;

        var options = new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            RecurseSubdirectories = false
        };

        foreach (var f in Directory.EnumerateFiles(m_directory, prefix + "*", options).Select(x => GetInfo(GetKeyFromPath(x))).Where(x => x != null))
            yield return f!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
