
using System.ComponentModel;

namespace KVPSButter.Filesystem;

/// <summary>
/// Loader factory for filesystem based KVPS implementation
/// </summary>
public class Loader : IKVPSFactory
{
    /// <summary>
    /// The default schemes suggested by the loader
    /// </summary>
    public IEnumerable<string> SupportedSchemes
        => new[] { "file", "local", "path" };

    /// <inheritdoc/>
    public IEnumerable<Option> SupportedOptions
        => ParsedConnectionString.GetSupportedOptions<Config>();

    /// <inheritdoc/>
    public string Description => "A file-based storage provider mapping to a folder in the local filesystem";

    /// <inheritdoc/>
    public string? UsageInstructions => null;

    /// <summary>
    /// The configuration options for the provider
    /// </summary>
    /// <param name="PathMapped">The path mapped option</param>
    private record Config(
        [Description("Enable mapping keys to local paths")]
        bool PathMapped = false
    );

    /// <inheritdoc/>
    public IKVPS Create(string connectionString)
    {
        var (parsed, config) = KVPSLoader.ParseConnectionString<Config>(connectionString);
        if (!Directory.Exists(parsed.RequirePath().Path))
            throw new DirectoryNotFoundException($"Cannot create a KVPS for non-existing root folder: {parsed.Path}");

        return config.PathMapped
            ? new KVPS(parsed.Path)
            : new KVPSEncoded(parsed.Path);
    }
}
