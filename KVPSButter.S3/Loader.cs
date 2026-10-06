
using System.ComponentModel;
using System.Reflection;
using Amazon.Runtime;
using Amazon.S3;

namespace KVPSButter.S3;

/// <summary>
/// Loader factory for S3 based KVPS implementation
/// </summary>
public class Loader : IKVPSFactory
{
    /// <summary>
    /// The default schemes suggested by the loader
    /// </summary>
    public IEnumerable<string> SupportedSchemes
        => new[] { "s3", "aws" };

    /// <inheritdoc/>
    public IEnumerable<Option> SupportedOptions
        => ParsedConnectionString.GetSupportedOptions<Config>();

    /// <inheritdoc/>
    public string Description => "An S3-compatible storage provider";

    /// <inheritdoc/>
    public string? UsageInstructions => @"Use the S3 connection format:
    s3://bucket.name/prefix?username=...&password=...
    ";

    private record Config(
        [Description("The S3 accessKey or username")]
        string Username,
        [Description("The S3 secretKey or password")]
        string Password,
        [Description("Override for the bucket name, using the URL bucket name if not provided")]
        string? Bucket = null,
        [Description("Override for the path prefix, using the URL after bucket name if not provided")]
        string? Prefix = null,
        [Description("The service URL if not using AWS S3")]
        string? ServiceUrl = null,
        [Description("Toggles the use of GetObjectAttributes")]
        bool DisableGetObjectAttributes = false,
        [Description("Forces the use of path-style URLs instead of virtual-hosted-style URLs")]
        bool ForcePathStyle = false,
        [Description("Disable chunked transfer encoding for uploads")]
        bool DisableChunkedEncoding = false,
        [Description("The checksum algorithm to send with uploads, one of the algorithms the AWS SDK knows (such as CRC32 or SHA256)")]
        string? ChecksumAlgorithm = null
    );

    /// <inheritdoc/>
    public IKVPS Create(string connectionString)
    {
        var (parsed, config) = KVPSLoader.ParseConnectionString<Config>(connectionString);
        var (username, password) = parsed.GetRequiredCredentials();
        var pathparts = parsed.Path.Split("/", 2, StringSplitOptions.None);
        var bucket = pathparts[0];
        var prefix = pathparts.Skip(1).LastOrDefault() ?? string.Empty;

        if (config.Bucket != null)
            bucket = config.Bucket;
        if (config.Prefix != null)
            prefix = config.Prefix;

        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOptionException("The bucket name is required");

        var s3cfg = new AmazonS3Config()
        {
            UseHttp = false,
            ForcePathStyle = config.ForcePathStyle
        };
        if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
            s3cfg.ServiceURL = config.ServiceUrl;

        var checksumAlgorithm = string.IsNullOrWhiteSpace(config.ChecksumAlgorithm)
            ? null
            : ParseChecksumAlgorithm(config.ChecksumAlgorithm);

        var client = new AmazonS3Client(new BasicAWSCredentials(username, password), s3cfg);
        return new KVPS(client, bucket, prefix, config.DisableGetObjectAttributes, config.DisableChunkedEncoding, checksumAlgorithm);
    }

    /// <summary>
    /// The checksum algorithms the AWS SDK defines, read from its constants so new ones are picked up with the SDK
    /// </summary>
    private static readonly IReadOnlyList<ChecksumAlgorithm> KnownChecksumAlgorithms = typeof(ChecksumAlgorithm)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.FieldType == typeof(ChecksumAlgorithm))
        .Select(x => (ChecksumAlgorithm)x.GetValue(null)!)
        .ToList();

    /// <summary>
    /// Finds the checksum algorithm named by an option value
    /// </summary>
    /// <param name="name">The option value, matched case-insensitively against the SDK's algorithm names</param>
    /// <returns>The algorithm</returns>
    /// <exception cref="InvalidOptionException">The SDK defines no algorithm with that name</exception>
    private static ChecksumAlgorithm ParseChecksumAlgorithm(string name)
        => KnownChecksumAlgorithms.FirstOrDefault(x => string.Equals(x.Value, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOptionException($"Unsupported checksum algorithm: {name}. Supported: {string.Join(", ", KnownChecksumAlgorithms.Select(x => x.Value))}");
}
