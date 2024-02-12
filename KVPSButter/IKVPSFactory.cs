namespace KVPSButter;

/// <summary>
/// Factory pattern for supporting different KVPS providers
/// </summary>
public interface IKVPSFactory
{
    /// <summary>
    /// The default schemes used by the provider.
    /// The implementation should not validate the scheme of the given connectionstring,
    /// such that the runtime defines the schemes
    /// </summary>
    IEnumerable<string> SupportedSchemes { get; }

    /// <summary>
    /// Gets the supported options for the factory
    /// </summary>
    IEnumerable<Option> SupportedOptions { get; }

    /// <summary>
    /// Human-readable description for the factory
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Human-readable usage description for the factory
    /// </summary>
    string? UsageInstructions { get; }

    /// <summary>
    /// Creates a new <see cref="IVFS"/> instance
    /// </summary>
    /// <param name="connectionString">The connectionstring passed</param>
    /// <returns>The created <see cref="IVFS"/> instance</returns>
    IKVPS Create(string connectionString);
}
