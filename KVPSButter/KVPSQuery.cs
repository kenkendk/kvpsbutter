
namespace KVPSButter;

/// <summary>
/// Represents a query that can be execute against a virtual filesystem
/// </summary>
/// <param name="Prefix">The prefix to match</param>
/// <param name="PageSize">The size of pages in a paginated query</param>
/// <param name="MaxResults">The maximum number of results to return</param>
/// <param name="Cursor">The cursor, if continuing a previously paginated query</param>
public record KVPSQuery(string? Prefix, int? PageSize = null, int? MaxResults = null, string? Cursor = null)
{
    /// <summary>
    /// The empty query
    /// </summary>
    public static readonly KVPSQuery Empty = new KVPSQuery(null, null, null, null);

    /// <summary>
    /// Returns a query that also applies a path prefix
    /// </summary>
    /// <param name="prefix">The prefix to apply</param>
    /// <returns>The new query</returns>
    public KVPSQuery WithPrefix(string prefix)
        => this with { Prefix = prefix };

    /// <summary>
    /// Evaluates locally if an entry would match the query
    /// </summary>
    /// <param name="key">The key to match</param>
    /// <returns><c>true</c>if there is a match; <c>false</c> otherwise</returns>
    public bool Matches(string key)
    {
        if (string.IsNullOrWhiteSpace(Prefix) || key.StartsWith(Prefix))
            return true;

        return false;
    }
}
