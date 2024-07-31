using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;

namespace KVPSButter.S3;

/// <summary>
/// Implementation of the <see cref="IKVPS"/> interface for S3 storage
/// </summary>
public class KVPS : IKVPS, IKVPSBatch
{
    /// <summary>
    /// The wrapped S3 client
    /// </summary>
    private readonly AmazonS3Client m_client;
    /// <summary>
    /// The configured S3 bucket
    /// </summary>
    private readonly string m_bucket;
    /// <summary>
    /// The configured entry prefix
    /// </summary>
    private readonly string m_prefix;
    /// <summary>
    /// Flag toggling the use og GetObjectAttributes
    /// </summary>
    private readonly bool m_disableGetObjectAttributes;
    /// <summary>
    /// The cursor prefix
    /// </summary>
    private const string CursorPrefix = "cv1";

    /// <summary>
    /// Creates a new KVPS instance
    /// </summary>
    /// <param name="client">The pre-configured client</param>
    /// <param name="bucket">The bucket name</param>
    /// <param name="prefix">The optional key prefix</param>
    /// <param name="disableGetObjectAttributes">Flag that toggles disabling the GetObjectAttributes method call</param>
    public KVPS(AmazonS3Client client, string bucket, string prefix, bool disableGetObjectAttributes)
    {
        m_client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentNullException(nameof(bucket));
        m_bucket = bucket;
        m_prefix = prefix ?? string.Empty;
        m_disableGetObjectAttributes = disableGetObjectAttributes;
    }

    /// <summary>
    /// Maps a key to the S3 path
    /// </summary>
    /// <param name="key">The key to map</param>
    /// <returns>The S3 path</returns>
    private string MapKeyToRemotePath(string key)
        => m_prefix + key;

    /// <summary>
    /// Maps an S3 path to a key
    /// </summary>
    /// <param name="path">The path to map</param>
    /// <returns>The mapped key</returns>
    private string MapRemoteKeyToPath(string path)
        => path.StartsWith(m_prefix)
            ? path.Substring(m_prefix.Length)
            : throw new InvalidKeyException("Path cannot be mapped to a key");

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => m_client.DeleteObjectAsync(m_bucket, MapKeyToRemotePath(key), cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        m_client.Dispose();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KVP> EnumerateAsync(KVPSQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        query ??= KVPSQuery.Empty;

        var remaining = query.MaxResults ?? int.MaxValue;
        var pagesize = query.PageSize ?? 1000;
        var continuationToken = string.Empty;
        var skip = 0;

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var parts = query.Cursor.Split(':', 3);
            if (parts.Length != 3 || parts[0] != CursorPrefix)
                throw new InvalidCursorException("Invalid cursor format");
            if (!int.TryParse(parts[1], out skip) || skip < 0)
                throw new InvalidCursorException("Invalid cursor format");

            continuationToken = parts[2];
        }

        while (remaining > 0)
        {
            var resp = await m_client.ListObjectsV2Async(new ListObjectsV2Request()
            {
                BucketName = m_bucket,
                ContinuationToken = continuationToken,
                Prefix = MapKeyToRemotePath(query.Prefix ?? string.Empty),
                MaxKeys = Math.Min(remaining, pagesize)
            }, cancellationToken).ConfigureAwait(false);

            var items = 0;
            foreach (var obj in resp.S3Objects)
            {
                items++;
                if (skip > 0)
                {
                    skip--;
                    continue;
                }

                remaining--;
                yield return new KVP(
                    MapRemoteKeyToPath(obj.Key),
                    obj.Size,
                    null,
                    obj.LastModified,
                    $"{CursorPrefix}:{items}:{continuationToken}",
                    obj.ETag,
                    null
                );

                if (remaining <= 0)
                    yield break;
            }

            continuationToken = resp.ContinuationToken;
            if (!resp.IsTruncated)
                yield break;
        }
    }

    /// <inheritdoc/>
    public async Task<KVP?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
    {
        if (m_disableGetObjectAttributes)
        {
            using var obj = await m_client.GetObjectAsync(new GetObjectRequest()
            {
                BucketName = m_bucket,
                Key = MapKeyToRemotePath(key),
                ByteRange = new ByteRange(0, 0)
            }, cancellationToken).ConfigureAwait(false);

            var length = obj.ContentLength;
            if (length <= 1 && obj.ContentRange.StartsWith("bytes 0-0/"))
                length = long.Parse(obj.ContentRange.Split('/', 2).Last());

            return new KVP(
                key,
                length,
                null,
                obj.LastModified,
                null,
                obj.ETag,
                null
            );
        }
        else
        {
            var resp = await m_client.GetObjectAttributesAsync(new GetObjectAttributesRequest()
            {
                BucketName = m_bucket,
                Key = MapKeyToRemotePath(key),
                ObjectAttributes = new List<ObjectAttributes>() {
                    ObjectAttributes.ObjectSize,
                    ObjectAttributes.ETag
                 }
            }, cancellationToken).ConfigureAwait(false);

            return new KVP(
                key,
                resp.ObjectSize,
                null,
                resp.LastModified,
                null,
                resp.ETag,
                null
            );
        }
    }

    /// <inheritdoc/>
    public async Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var resp = await m_client.GetObjectAsync(m_bucket, MapKeyToRemotePath(key), cancellationToken).ConfigureAwait(false);
        return resp.ResponseStream;
    }

    /// <inheritdoc/>
    public Task WriteAsync(string key, Stream data, CancellationToken cancellationToken = default)
    {
        return m_client.PutObjectAsync(new PutObjectRequest()
        {
            BucketName = m_bucket,
            Key = MapKeyToRemotePath(key),
            InputStream = data
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, KVP? Entry)> GetInfoAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => KVPSBatchExtender.GetInfoAsync(this, keys, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<(string Key, Stream? stream)> ReadAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => KVPSBatchExtender.ReadAsync(this, keys, cancellationToken);

    /// <inheritdoc/>
    public Task WriteAsync(IEnumerable<(string Key, Stream Stream)> values, CancellationToken cancellationToken)
        => KVPSBatchExtender.WriteAsync(this, values, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        => m_client.DeleteObjectsAsync(new DeleteObjectsRequest()
        {
            BucketName = m_bucket,
            Objects = keys.Select(x => new KeyVersion() { Key = MapKeyToRemotePath(x) }).ToList()
        }, cancellationToken);
}
