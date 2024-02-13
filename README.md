# KVPSButter - Abstraction layer for key-value-pair storage

This project adds a simple abstraction for a key-value-pair based storage, similar to how a dictionary works in most programming languages. The interface is designed to work with network based storage but can also work with local storage, such as databases or filesystems.

## Why KVPSButter ?

A common requirement for any program is to be able to read and write some kind of persitent storage. Traditionally, this has been done to local disk, but with virtualization, such as [Docker](https://www.docker.com) this can be cumbersome in terms of backup and synchronization.

One approach to this has been to use [FUSE](https://github.com/libfuse/libfuse) which makes the filesystem-layer the abstraction. While this approach works because it requires little (if any) change to the application it is also suboptimal as it is exposed to the operation system and requires kernel support.

KVPSButter solves this problem by letting the storage destination be specified runtime, such that multiple applications can collaborate without writing applications for a specific storage. This does require that the application uses KVPSButter and is structured around a key-value-pair based storage, but besides this requirement, it is transparent to the application what implementation is actually used.

## Examples

To get a connection, use the default loader:

```
var kvps = KVPSLoader.Default.Create("memory://");
```

From here it is possible to interact with the store:

```
await kvps.WriteAsync("key1", new MemoryStream(new byte[] { 1, 2, 3 }));
var data = kvps.ReadAsync("key1");
```

## Destinations

As the key-value-pair abstraction is quite simple, most kinds of storage should support this. At the time of writing, the following are supported:

- `test://`, `memory://` stores data in-memory with no permant storage. Mostly for testing an temporary data

- `file://`, `local://`, `path://` stores data on the filesystem. This destination is also fairly simple, but will default to encode the keys with base64 to avoid illegal characters

- `s3`, `aws` store data on an S3 compatible destination


