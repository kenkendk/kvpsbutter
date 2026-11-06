using System.Runtime.CompilerServices;

namespace KVPSButter;

/// <summary>
/// A key prefix found by <see cref="IKVPSPrefixEnumeration.EnumeratePrefixesAsync"/>
/// </summary>
/// <param name="Prefix">The prefix, ending with the delimiter</param>
/// <param name="Cursor">The cursor that resumes the enumeration after this prefix</param>
public record KVPPrefix(string Prefix, string? Cursor);

/// <summary>
/// Optional interface for stores that can list the distinct key prefixes at one level, like folders,
/// without listing every key below them
/// </summary>
public interface IKVPSPrefixEnumeration : IKVPS
{
    /// <summary>
    /// Lists the distinct prefixes directly below <see cref="KVPSQuery.Prefix"/>. For a key below the query prefix,
    /// its prefix is everything up to and including the first <paramref name="delimiter"/> after the query prefix.
    /// Keys without a delimiter after the query prefix are not returned.
    /// </summary>
    /// <param name="query">The optional query; its prefix, page size, result limit and cursor are used</param>
    /// <param name="delimiter">The delimiter separating the levels, for example <c>/</c></param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The distinct prefixes, each with a cursor to resume after it</returns>
    IAsyncEnumerable<KVPPrefix> EnumeratePrefixesAsync(KVPSQuery? query, string delimiter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Prefix enumeration for any <see cref="IKVPS"/>, using the native support where the store has it
/// </summary>
public static class KVPSPrefixEnumeration
{
    /// <summary>
    /// The cursor prefix used when the prefixes are found by scanning the keys
    /// </summary>
    private const string ScanCursorPrefix = "ps1";

    /// <summary>
    /// Lists the distinct prefixes directly below the query prefix. A store that implements
    /// <see cref="IKVPSPrefixEnumeration"/> lists them natively; any other store is scanned key by key,
    /// which reads every key below the query prefix.
    /// </summary>
    /// <param name="store">The store to list</param>
    /// <param name="query">The optional query; its prefix, page size, result limit and cursor are used</param>
    /// <param name="delimiter">The delimiter separating the levels, for example <c>/</c></param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The distinct prefixes, each with a cursor to resume after it</returns>
    public static IAsyncEnumerable<KVPPrefix> EnumeratePrefixesAsync(this IKVPS store, KVPSQuery? query, string delimiter, CancellationToken cancellationToken = default)
        => store is IKVPSPrefixEnumeration native
            ? native.EnumeratePrefixesAsync(query, delimiter, cancellationToken)
            : ScanPrefixesAsync(store, query, delimiter, cancellationToken);

    /// <summary>
    /// Lists the distinct prefixes by reading every key below the query prefix. The cursor names the last
    /// returned prefix, so resuming relies on the store enumerating keys in a stable order.
    /// </summary>
    /// <param name="store">The store to scan</param>
    /// <param name="query">The optional query; its prefix, page size, result limit and cursor are used</param>
    /// <param name="delimiter">The delimiter separating the levels, for example <c>/</c></param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The distinct prefixes, each with a cursor to resume after it</returns>
    public static async IAsyncEnumerable<KVPPrefix> ScanPrefixesAsync(IKVPS store, KVPSQuery? query, string delimiter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(delimiter))
            throw new ArgumentException("The delimiter must not be empty", nameof(delimiter));

        query ??= KVPSQuery.Empty;
        var basePrefix = query.Prefix ?? string.Empty;
        var remaining = query.MaxResults ?? int.MaxValue;

        string? resumeAfter = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var parts = query.Cursor.Split(':', 2);
            if (parts.Length != 2 || parts[0] != ScanCursorPrefix)
                throw new InvalidCursorException("Invalid cursor format");
            resumeAfter = parts[1];
        }

        if (remaining <= 0)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var entry in store.EnumerateAsync(new KVPSQuery(basePrefix, query.PageSize, null, null), cancellationToken))
        {
            if (!entry.Key.StartsWith(basePrefix, StringComparison.Ordinal))
                continue;

            var index = entry.Key.IndexOf(delimiter, basePrefix.Length, StringComparison.Ordinal);
            if (index < 0)
                continue;

            var prefix = entry.Key.Substring(0, index + delimiter.Length);
            if (!seen.Add(prefix))
                continue;

            // Prefixes up to and including the cursor were returned before
            if (resumeAfter != null)
            {
                if (prefix == resumeAfter)
                    resumeAfter = null;
                continue;
            }

            yield return new KVPPrefix(prefix, $"{ScanCursorPrefix}:{prefix}");
            if (--remaining <= 0)
                yield break;
        }
    }
}
