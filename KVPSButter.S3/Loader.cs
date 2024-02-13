
using System.ComponentModel;
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
        string? ServiceUrl = null
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

        var s3cfg = new AmazonS3Config() { UseHttp = false };
        if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
            s3cfg.ServiceURL = config.ServiceUrl;

        var client = new AmazonS3Client(new BasicAWSCredentials(username, password), s3cfg);
        return new KVPS(client, bucket, prefix);
    }
}
