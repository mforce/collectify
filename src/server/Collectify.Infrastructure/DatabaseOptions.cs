namespace Collectify.Infrastructure;

/// <summary>
/// Database provider selection. Bound from <c>Collectify:Database</c> config
/// section (env vars: <c>Collectify__Database__*</c>).
/// </summary>
public static class DatabaseOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Collectify:Database";

    /// <summary>Default provider.</summary>
    public const string DefaultProvider = "sqlite";

    /// <summary>Supported provider identifiers.</summary>
    public static readonly HashSet<string> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite",
        "postgres",
    };
}
