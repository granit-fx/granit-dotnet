namespace Granit.Settings.Providers;

/// <summary>
/// Manager of setting value providers, sorted by priority order.
/// </summary>
public sealed class SettingValueProviderRegistry(IEnumerable<ISettingValueProvider> providers)
{
    /// <summary>
    /// Ordered list of providers, from highest priority (U=100) to lowest priority (D=500).
    /// </summary>
    public IReadOnlyList<ISettingValueProvider> Providers { get; } = [.. providers.OrderBy(p => p.Order)];

    /// <summary>
    /// Returns the provider by its short name, or <c>null</c> if not registered.
    /// </summary>
    public ISettingValueProvider? GetOrNull(string name) =>
        Providers.FirstOrDefault(p => p.Name == name);
}
