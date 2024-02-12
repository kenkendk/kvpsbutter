using System.ComponentModel;

namespace KVPSButter.SyncTool;

/// <summary>
/// The options supported by the <see cref="FSWatcher"/>
/// </summary>
/// <param name="Path">The path being watched</param>
/// <param name="IgnoreDelete">Toggle flag for ignoring deletes</param>
/// <param name="CopyExisting">Start with copying the existing files</param>
/// <param name="Recurse">Enable recursing directories</param>
public record FSWatcherOptions(
    [Description("The path to watch for changes")]
    string Path,
    [Description("Toggles propagating deleted files to deleting values in the KVPS")]
    bool IgnoreDelete = true,
    [Description("Copies any existing files before monitoring")]
    bool CopyExisting = false,
    [Description("Toggles recursing the subfolders")]
    bool Recurse = true
);

/// <summary>
/// The file-system watcher wrapper
/// </summary>
public class FSWatcher : IDisposable
{
    /// <summary>
    /// The <see cref="IKVPS"/> instance
    /// </summary>
    private readonly IKVPS m_kvps;
    /// <summary>
    /// The <see cref="FileSystemWatcher"/> instance
    /// </summary>
    private readonly FileSystemWatcher m_watcher;
    /// <summary>
    /// The configured options
    /// </summary>
    private readonly FSWatcherOptions m_options;
    /// <summary>
    /// The cancellation token source
    /// </summary>
    private readonly CancellationTokenSource m_cancellationTokenSource = new();
    /// <summary>
    /// The time to wait after a change has been detected
    /// </summary>
    private readonly TimeSpan ChangeWaitTime = TimeSpan.FromSeconds(2);
    /// <summary>
    /// The list of active timers
    /// </summary>
    private readonly Dictionary<string, DebounceTimer> m_timers = new Dictionary<string, DebounceTimer>();
    /// <summary>
    /// The lock guarding <see cref="m_timers"/>
    /// </summary>
    private readonly object m_lock = new();

    /// <summary>
    /// Creates a new <see cref="FSWatcher"/>
    /// </summary>
    /// <param name="options">The options to use</param>
    /// <param name="kvps">The <see cref="IKVPS"/> instance</param>
    public FSWatcher(FSWatcherOptions options, IKVPS kvps)
    {
        m_kvps = kvps ?? throw new ArgumentNullException(nameof(kvps));
        m_options = options ?? throw new ArgumentNullException(nameof(options));
        m_watcher = new FileSystemWatcher
        {
            IncludeSubdirectories = options.Recurse
        };

        m_watcher.Created += OnChanged;
        m_watcher.Changed += OnChanged;
        m_watcher.Deleted += OnDeleted;
        m_watcher.Renamed += OnRenamed;
    }

    /// <summary>
    /// Maps the local path to a key
    /// </summary>
    /// <param name="localPath">The path to map</param>
    /// <returns>The mapped key or <c>null</c> if the path cannot be mapped</returns>
    private string? MapLocalPathToKey(string localPath)
    {
        if (!localPath.StartsWith(m_options.Path))
            return null;
        return localPath.Substring(m_options.Path.Length + (m_options.Path.EndsWith(Path.DirectorySeparatorChar) ? 0 : 1));
    }

    /// <summary>
    /// Handler for the debouncer expiration
    /// </summary>
    /// <param name="key">The key to use</param>
    /// <param name="path">The path to file data</param>
    private async void DebouncerExpired(string key, string path)
    {
        if (m_cancellationTokenSource.IsCancellationRequested)
            return;
        lock (m_lock)
            m_timers.Remove(key);

        try
        {
            await m_kvps.WriteAsync(key, File.OpenRead(path), m_cancellationTokenSource.Token);
        }
        catch
        {
            // Retry? Need to keep a retry count
            // OnChanged(...);
        }
    }

    /// <summary>
    /// The change event handler
    /// </summary>
    /// <param name="o">Unused object</param>
    /// <param name="args">The change args</param>
    private void OnChanged(object o, FileSystemEventArgs args)
    {
        var path = args.FullPath;
        var key = MapLocalPathToKey(path);
        if (key == null)
            return;

        lock (m_lock)
        {
            if (m_timers.TryGetValue(key, out var timer) && timer != null)
                timer.Reset();
            else
                m_timers.Add(key, new DebounceTimer(() => DebouncerExpired(key, path), ChangeWaitTime));
        }
    }

    /// <summary>
    /// The delete event handler
    /// </summary>
    /// <param name="o">Unused object</param>
    /// <param name="args">The delete args</param>
    private async void OnDeleted(object o, FileSystemEventArgs args)
    {
        if (m_options.IgnoreDelete)
            return;

        var key = MapLocalPathToKey(args.FullPath);
        if (key != null)
            await m_kvps.DeleteAsync(key, m_cancellationTokenSource.Token);
    }

    /// <summary>
    /// The rename event handler
    /// </summary>
    /// <param name="o">Unused object</param>
    /// <param name="args">The rename args</param>
    private void OnRenamed(object o, RenamedEventArgs args)
    {
        OnChanged(o, args);
        OnDeleted(o, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(args.OldFullPath)!, args.OldName));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        m_watcher.EnableRaisingEvents = false;
        m_cancellationTokenSource.Cancel();
        m_watcher.Dispose();
        m_kvps.Dispose();
    }

    /// <summary>
    /// Starts the watcher
    /// </summary>
    /// <returns>An awaitable task</returns>
    public async Task Start()
    {
        m_watcher.Path = m_options.Path;
        if (m_options.CopyExisting)
        {
            foreach (var p in Directory.EnumerateFiles(m_options.Path, "*", m_options.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                var key = MapLocalPathToKey(p);
                if (key != null)
                    await m_kvps.WriteAsync(key, File.OpenRead(p), m_cancellationTokenSource.Token);
            }

            m_watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Stops the watcher
    /// </summary>
    public void Stop()
    {
        m_watcher.EnableRaisingEvents = false;
    }
}
