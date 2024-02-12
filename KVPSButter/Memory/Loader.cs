namespace KVPSButter.Memory;

/// <summary>
/// Loader factory for filesystem based KVPS implementation
/// </summary>
public class Loader : IKVPSFactory
{
    /// <summary>
    /// The default schemes suggested by the loader
    /// </summary>
    public IEnumerable<string> SupportedSchemes
        => new[] { "memory", "test" };

    /// <inheritdoc/>
    public IEnumerable<Option> SupportedOptions
        => Enumerable.Empty<Option>();

    /// <inheritdoc/>
    public string Description => "An in-memory provider with no persistence";

    /// <inheritdoc/>
    public string? UsageInstructions => null;

    /// <inheritdoc/>
    public IKVPS Create(string connectionString)
    {
        // No configuration possible with in-memory KVPS
        return new KVPS();
    }
}