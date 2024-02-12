using System.Reflection;

namespace KVPSButter;

/// <summary>
/// Loader implementation for <see cref="IKVPSFactory"/>
/// </summary>
public class KVPSLoader
{
    /// <summary>
    /// The lock guarding the factories
    /// </summary>
    private readonly object m_lock = new();

    /// <summary>
    /// The list of supported schemes
    /// </summary>
    private Dictionary<string, IKVPSFactory> m_factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a <see cref="IKVPSFactory"/> with the default schemes
    /// </summary>
    /// <param name="factory">The factory to register</param>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader Register(IKVPSFactory factory)
        => Register(factory, factory.SupportedSchemes);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="schemes"></param>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader Register(IKVPSFactory factory, IEnumerable<string> schemes)
    {
        lock (m_lock)
            foreach (var sc in schemes)
                m_factories[sc] = factory;

        return this;
    }

    /// <summary>
    /// Creates a new <see cref="KVPSButter"> instance
    /// </summary>
    /// <param name="connectionString">The connection string to use</param>
    /// <returns>The created instance</returns>
    public IKVPS Create(string connectionString)
    {
        var parts = connectionString.Split("://", 2, StringSplitOptions.None);
        if (parts.Length != 2)
            throw new ArgumentException($"Connection string format invalid");

        var scheme = parts[0];

        IKVPSFactory? factory;
        lock (m_lock)
            m_factories.TryGetValue(scheme, out factory);

        if (factory == null)
            throw new ArgumentException($"No provider found for {scheme}://");

        return factory.Create(connectionString);
    }

    /// <summary>
    /// Returns the current set of providers
    /// </summary>
    public IReadOnlyDictionary<string, IKVPSFactory> Providers => m_factories;

    /// <summary>
    /// Loads the default providers
    /// </summary>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader RegisterDefaults()
    {
        Register(new Filesystem.Loader());
        Register(new Memory.Loader());
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KVPSBUTTER_NOSCANNING")))
            RegisterBundledAssemblies();
        return this;
    }

    /// <summary>
    /// Scans all loaded assemblies for <see cref="IKVPSFactory"/> implementations
    /// </summary>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader RegisterLoadedAssemblies()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            RegisterFromAssembly(asm);
        return this;
    }

    /// <summary>
    /// Registers all <see cref="IKVPSFactory"/> types with a default constructor from the assembly
    /// </summary>
    /// <param name="assembly">The assembly to search</param>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader RegisterFromAssembly(Assembly assembly)
    {
        var instances = assembly.ExportedTypes
            .Where(x => x.IsClass && x.GetInterfaces().Any(y => y == typeof(IKVPSFactory)))
            .Where(x => x.GetConstructor(Type.EmptyTypes) != null)
            .ToList();

        foreach (var inst in instances)
            try
            {
                var f = Activator.CreateInstance(inst) as IKVPSFactory;
                if (f != null)
                    Register(f);
            }
            catch
            {
            }

        return this;
    }

    /// <summary>
    /// Scans all assemblies in the source folder for <see cref="IKVPSFactory"/> implementations
    /// </summary>
    /// <returns>The loader for chaining calls</returns>
    public KVPSLoader RegisterBundledAssemblies()
    {
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
                try
                {
                    RegisterFromAssembly(Assembly.LoadFile(file));
                }
                catch
                {
                }
        }
        return this;
    }

    /// <summary>
    /// Creates a new <see cref="IKVPS"/> instance using the default loader
    /// </summary>
    /// <param name="connectionString">The connection string to use</param>
    /// <returns>The created instance</returns>
    public static IKVPS CreateIKVPS(string connectionString)
        => Default.Create(connectionString);

    /// <summary>
    /// The default loader instance
    /// </summary>
    public static KVPSLoader Default = new KVPSLoader();

    /// <summary>
    /// Static initializer, registers the built-in providers in the default loader
    /// </summary>
    static KVPSLoader()
    {
        Default.RegisterDefaults();
    }

    /// <summary>
    /// Helper method that provides a shared way of parsing connectionstrings
    /// </summary>
    /// <param name="connectionString">The connectionstring to parse</param>
    /// <returns>The parsed instance</returns>
    public static ParsedConnectionString ParseConnectionString(string connectionString)
        => ParsedConnectionString.Parse(connectionString);

    /// <summary>
    /// Parses the connection string into path and typed config instance
    /// </summary>
    /// <typeparam name="T">The type to map to; assumed to be a record</typeparam>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns></returns>
    public static (ParsedConnectionString Parsed, T Config) ParseConnectionString<T>(string connectionString)
        => ParsedConnectionString.Parse<T>(connectionString);

    /// <summary>
    /// Extracts the supported options for a record-like type
    /// </summary>
    /// <typeparam name="T">The record-like type to map</typeparam>
    /// <returns>The supported options</returns>
    public static IEnumerable<Option> GetSupportedOptions<T>()
        => ParsedConnectionString.GetSupportedOptions<T>();
}

/// <summary>
/// Extension methods for <see cref="KVPSLoader"/> instances
/// </summary>
public static class IKVPSLoaderExtensions
{
    /// <summary>
    /// Registers the factory on a loader, using the default loader if none is specified
    /// </summary>
    /// <param name="factory">The <see cref="IKVPSFactory"/> to register</param>
    /// <param name="loader">The optional loader to register with</param>
    /// <returns>The factory for chaining calls</returns>
    public static IKVPSFactory Register(this IKVPSFactory factory, KVPSLoader? loader = null)
    {
        (loader ?? KVPSLoader.Default).Register(factory);
        return factory;
    }

    /// <summary>
    /// Registers the factory on a loader, using the default loader if none is specified
    /// </summary>
    /// <param name="factory">The <see cref="IKVPSFactory"/> to register</param>
    /// <param name="schemes">The schemes to register with</param>
    /// <param name="loader">The optional loader to register with</param>
    /// <returns>The factory for chaining calls</returns>
    public static IKVPSFactory Register(this IKVPSFactory factory, IEnumerable<string> schemes, KVPSLoader? loader = null)
    {
        (loader ?? KVPSLoader.Default).Register(factory, schemes);
        return factory;
    }
}
