# SyncTool

This is an example usage of the KVPS to store copies of files in an IKVPS backed store.

## Implementation notes

The tool uses a [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-8.0) to monitor changes to a folder, and mimics these changes into the IKVPS storage.
