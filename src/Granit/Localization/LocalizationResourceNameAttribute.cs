namespace Granit.Localization;

/// <summary>
/// Associates a short name with a localization resource marker class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class LocalizationResourceNameAttribute(string name) : Attribute
{
    /// <summary>
    /// Short name of the resource (e.g. "Granit", "Vault").
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Default culture of the resource, used as the final fallback.
    /// Used by auto-discovery when no explicit registration is present.
    /// </summary>
    public string DefaultCulture { get; init; } = "en";
}
