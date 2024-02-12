namespace KVPSButter;

/// <summary>
/// Entry describing a key-value-pair
/// </summary>
/// <param name="Key">The key for the entry</param>
/// <param name="Length">The content length</param>
/// <param name="Created">The creation timestamp</param>
/// <param name="LastModified">The last modified timestamp</param>
/// <param name="Cursor">The cursor, if this entry was created with pagination</param>
/// <param name="Etag">The item etag or hash</param>
/// <param name="Extra">Provider specific extra data</param>
public record KVP(
    string Key,
    long? Length,
    DateTime? Created,
    DateTime? LastModified,
    string? Cursor,
    string? Etag,
    object? Extra
);
