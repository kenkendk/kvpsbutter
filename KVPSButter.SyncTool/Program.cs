using System.CommandLine;
using KVPSButter;
using KVPSButter.SyncTool;

var infoCommand = new Command("info", "Returns information about available destinations");

foreach (var provider in KVPSLoader.Default.Providers)
{
    var prvCommand = new Command(provider.Key, provider.Value.Description);
    prvCommand.SetHandler(() =>
    {
        Console.WriteLine($"{provider.Key}: {provider.Value.Description}");
        Console.WriteLine("");
        Console.WriteLine("Options: ");
        foreach (var so in provider.Value.SupportedOptions)
        {
            Console.WriteLine($"  {so.Name}: {so.Description}");
            Console.WriteLine($"    {(so.Optional ? $"[optional]={so.Default}" : "<required>")}");
        }

        if (!string.IsNullOrWhiteSpace(provider.Value.UsageInstructions))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine(provider.Value.UsageInstructions);
        }
    });
    infoCommand.Add(prvCommand);
}

var rootFolderOption = new Option<DirectoryInfo>(name: "directory", description: "The directory to monitor");
var destinationOption = new Option<string>(name: "destination", description: "The IKVPS connection string");
var preCopyOption = new Option<bool>(name: "--pre-copy", description: "Copies all existing files before monitoring", getDefaultValue: () => false);
var recurseOption = new Option<bool>(name: "--recurse", description: "Toggles monitoring on all sub-directories", getDefaultValue: () => true);
var ignoreDeleteOption = new Option<bool>(name: "--ignore-delete", description: "Ignores delete operations, only creating data", getDefaultValue: () => true);

var monitorCommand = new Command("monitor")
{
    rootFolderOption,
    destinationOption,
    preCopyOption,
    recurseOption,
    ignoreDeleteOption
};

monitorCommand.SetHandler((root, destination, precopy, recurse, ignoreDelete) =>
{
    if (!root.Exists)
    {
        Console.WriteLine($"Path not found: {root.FullName}");
        return;
    }

    using var fsw = new FSWatcher(
        new FSWatcherOptions(
            Path: root.FullName,
            IgnoreDelete: ignoreDelete,
            CopyExisting: precopy,
            Recurse: recurse
        ),
        KVPSLoader.CreateIKVPS(destination)
    );
    var _ = fsw.Start();

    Console.WriteLine("Started watcher, stop by pressing enter");
    Console.ReadLine();

}, rootFolderOption, destinationOption, preCopyOption, recurseOption, ignoreDeleteOption);

return new RootCommand("IKVPS Filesystem Sync Tool")
{
    monitorCommand,
    infoCommand
}.Invoke(Environment.GetCommandLineArgs().Skip(1).ToArray());