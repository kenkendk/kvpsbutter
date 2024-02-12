namespace KVPSButter;

/// <summary>
/// A supported option
/// </summary>
/// <param name="Name">The name of the option</param>
/// <param name="Description">The description of the option</param>
/// <param name="Optional">Flag indicating if the option is optional</param>
/// <param name="Default">The default value for the option</param>
public record Option(string Name, string? Description, bool Optional, object? Default);
