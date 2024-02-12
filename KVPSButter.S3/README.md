# KVPSButter.S3 - S3 storage for KVPSButter

This package contains the S3 bindings for KVPSButter.

## Examples

To get a connection, use the default loader:

```
var kvps = KVPSLoader.Default.Create("s3://?username=accessKey&password=secretKey&bucket=bucket");
```

From here it is possible to interact with the store:

```
await kvps.WriteAsync("key1", new MemoryStream(new byte[] { 1, 2, 3 }));
var data = kvps.ReadAsync("key1");
```

## Supported options:

```
Username: The S3 accessKey or username
Password: The S3 secretKey or password
Bucket: Override for the bucket name, using the URL bucket name if not provided
Prefix: Override for the path prefix, using the URL after bucket name if not provided
ServiceUrl: The service URL if not using AWS S3
```