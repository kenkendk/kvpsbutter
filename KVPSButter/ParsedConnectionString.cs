using System.ComponentModel;
using System.Reflection;

namespace KVPSButter;

/// <summary>
/// Parsed connection string options for common handling of connection strings
/// </summary>
/// <param name="Scheme">The parsed scheme</param>
/// <param name="Path">The parsed path</param>
/// <param name="Options">The parsed options</param>
public record ParsedConnectionString(
    string Scheme,
    string Path,
    IReadOnlyDictionary<string, string> Options
)
{
    /// <summary>
    /// Option names that are used by multiple IKVPS implementations
    /// </summary>
    public static class StandardOptionNames
    {
        /// <summary>
        /// The username option
        /// </summary>
        public const string Username = "username";
        /// <summary>
        /// The password option
        /// </summary>
        public const string Password = "password";
        /// <summary>
        /// The port option
        /// </summary>
        public const string Port = "port";
    }

    /// <summary>
    /// Gets the username and password or throws if they are not present
    /// </summary>
    /// <returns>The username and password</returns>
    public (string Username, string Password) GetRequiredCredentials()
    {
        var username = GetOptionWithDefault<string>(StandardOptionNames.Username, null);
        var password = GetOptionWithDefault<string>(StandardOptionNames.Password, null);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOptionException($"The options \"{StandardOptionNames.Username}\" and \"{StandardOptionNames.Password}\" are required");

        return (username, password);
    }

    /// <summary>
    /// Checks that the path is set on the instance
    /// </summary>
    /// <returns>The instance for chaining calls</returns>
    public ParsedConnectionString RequirePath()
        => string.IsNullOrWhiteSpace(Path)
            ? throw new InvalidOptionException("The path was not set, but is required")
            : this;

    /// <summary>
    /// Gets the option with the name, or throws if it is not present
    /// </summary>
    /// <typeparam name="T">The value type to find</typeparam>
    /// <param name="name">The option name</param>
    /// <returns>The parsed option</returns>
    public T? GetRequiredOption<T>(string name)
        => (T?)GetRequiredOption(typeof(T), name);

    /// <summary>
    /// Gets the option with the name, or throws if it is not present
    /// </summary>
    /// <param name="type">The value type to find</param>
    /// <param name="name">The option name</param>
    /// <returns>The parsed option</returns>
    public object? GetRequiredOption(Type type, string name)
    {
        if (!Options.TryGetValue(name, out var v) || string.IsNullOrWhiteSpace(v))
            throw new InvalidOptionException($"The required option \"{name}\" is missing");

        return Parse(type, name, v);
    }

    /// <summary>
    /// Parses the option into the target type
    /// </summary>
    /// <typeparam name="T">The option type to return</typeparam>
    /// <param name="name">The option name for error messages</param>
    /// <param name="value">The value being parsed</param>
    /// <returns>The parsed option</returns>
    public T? Parse<T>(string name, string value)
        => (T?)Parse(typeof(T), name, value);

    /// <summary>
    /// Parses the option into the target type
    /// </summary>
    /// <param name="type">The option type to return</param>
    /// <param name="name">The option name for error messages</param>
    /// <param name="value">The value being parsed</param>
    /// <returns>The parsed option</returns>
    public object? Parse(Type type, string name, string value)
    {
        value = value.Trim();
        try
        {
            if (type == typeof(string))
            {
                return value;
            }
            else if (type == typeof(bool))
            {
                var yes = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

                var no = string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase);

                if (yes)
                    return true;
                else if (no)
                    return false;

            }
            else if (type.IsEnum)
            {
                if (Enum.TryParse(type, value, true, out var r))
                    return r;
            }
            else
            {
                return Convert.ChangeType(value, type);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOptionException($"Option {name} of type {type} could not be parsed", ex);
        }

        throw new InvalidOptionException($"Option {name} of type {type} could not be parsed");
    }

    /// <summary>
    /// Parses an option and uses a default value if it is missing
    /// </summary>
    /// <typeparam name="T">The type to parse to</typeparam>
    /// <param name="name">The option name</param>
    /// <param name="defaultvalue">The value to use if the option is missing</param>
    /// <returns>The parsed value</returns>
    public T? GetOptionWithDefault<T>(string name, T? defaultvalue = default)
        => (T?)GetOptionWithDefault(typeof(T), name, defaultvalue);

    /// <summary>
    /// Parses an option and uses a default value if it is missing
    /// </summary>
    /// <param name="type">The type to parse to</param>
    /// <param name="name">The option name</param>
    /// <param name="defaultvalue">The value to use if the option is missing</param>
    /// <returns>The parsed value</returns>
    private object? GetOptionWithDefault(Type type, string name, object? defaultvalue)
    {
        if (!Options.TryGetValue(name, out var v))
            return defaultvalue;

        return Parse(type, name, v);
    }

    /// <summary>
    /// Gets the target constructor for a record-like class
    /// </summary>
    /// <typeparam name="T">The record-like type to examine</typeparam>
    /// <returns>The matching constructor</returns>
    private static ConstructorInfo GetTargetConstructor<T>()
    {
        var constructor =
            typeof(T).GetConstructors()
                .Where(x => x.GetParameters().All(y => y.Name != null))
                .OrderByDescending(x => x.GetParameters().Count())
                .FirstOrDefault();

        if (constructor == null || constructor.GetParameters().Length == 0)
            throw new ArgumentException($"Unable to find suitable constructor for type {typeof(T).FullName}");

        return constructor;
    }

    /// <summary>
    /// Maps the options to a typed instance
    /// </summary>
    /// <typeparam name="T">The type to map to; assumed to be a record</typeparam>
    /// <returns>The type instance</returns>
    public T Map<T>()
    {
        var constructor = GetTargetConstructor<T>();
        var arguments = constructor.GetParameters()
            .Select(x =>
                 x.HasDefaultValue
                    ? GetOptionWithDefault(x.ParameterType, x.Name!, x.DefaultValue)
                    : GetRequiredOption(x.ParameterType, x.Name!)
            )
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }

    /// <summary>
    /// Parses the connection string into path and typed config instance
    /// </summary>
    /// <typeparam name="T">The type to map to; assumed to be a record</typeparam>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns></returns>
    public static (ParsedConnectionString Parsed, T Config) Parse<T>(string connectionString)
    {
        var cfg = Parse(connectionString);
        return (cfg, cfg.Map<T>());
    }

    /// <summary>
    /// Extracts the supported options for a record-like type
    /// </summary>
    /// <typeparam name="T">The record-like type to map</typeparam>
    /// <returns>The supported options</returns>
    public static IEnumerable<Option> GetSupportedOptions<T>()
    {
        var constructor = GetTargetConstructor<T>();
        return constructor.GetParameters()
            .Select(x => new Option(x.Name!, GetDescription(x), x.HasDefaultValue, x.HasDefaultValue ? x.DefaultValue : null))
            .ToArray();

        string? GetDescription(ParameterInfo pi)
            => pi.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }

    /// <summary>
    /// Parses a connection string into a typed component
    /// </summary>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns>The <see cref="ParsedConnectionString"/></returns>
    public static ParsedConnectionString Parse(string connectionString)
    {
        var initial = connectionString.Split("://", 2, StringSplitOptions.TrimEntries);
        if (initial.Length != 2)
            return new ParsedConnectionString(initial[0], string.Empty, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var second = initial[1].Split("?", 2, StringSplitOptions.TrimEntries);
        if (second.Length != 2)
            return new ParsedConnectionString(initial[0], second[0], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var parameters = second[1].Split("&", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split("=", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(x => x.Length == 2 ? x : new string[] { x[0], string.Empty })
            .Select(x => (Uri.UnescapeDataString(x[0]), Uri.UnescapeDataString(x[1])))
            .GroupBy(x => x.Item1, x => x.Item2);

        return new ParsedConnectionString(
            initial[0],
            second[0],
            parameters.ToDictionary(x => x.Key, y => y.Last(), StringComparer.OrdinalIgnoreCase));
    }
}
