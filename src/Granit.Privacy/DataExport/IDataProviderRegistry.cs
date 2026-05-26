namespace Granit.Privacy.DataExport;

/// <summary>
/// Declarative registry of modules participating in data subject export/deletion.
/// Each module registers its provider name (and optional Takeout-style metadata) at
/// startup. The saga uses <see cref="Count"/> to know how many fragments to expect;
/// <see cref="IPrivacyScopeResolver"/> uses <see cref="GetAllRegistrations"/> to evaluate
/// visibility gates.
/// </summary>
public interface IDataProviderRegistry
{
    /// <summary>
    /// Registers a data provider by name only — minimal metadata, no scope-selector
    /// visibility. Used for legacy registrations (e.g. tests).
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="Register(ProviderRegistration)"/> when the typed provider is
    /// available (it propagates <c>DisplayKey</c>, <c>FeatureName</c>, and the
    /// <c>HasData</c> probe used by the scope selector).
    /// </remarks>
    void Register(string providerName);

    /// <summary>
    /// Registers a fully-described data provider. Captures the static metadata
    /// (display key, feature name) and the probe delegate used by the scope selector.
    /// </summary>
    void Register(ProviderRegistration registration);

    /// <summary>Returns all registered provider names.</summary>
    IReadOnlyList<string> GetAll();

    /// <summary>
    /// Returns the full registration records for every provider that registered with
    /// <see cref="Register(ProviderRegistration)"/>. Providers registered name-only
    /// (legacy path) are returned with a default registration: <c>DisplayKey</c> equal
    /// to the provider name, <c>FeatureName</c> null, <c>HasDataProbe</c> returning
    /// <see langword="true"/>.
    /// </summary>
    IReadOnlyList<ProviderRegistration> GetAllRegistrations();

    /// <summary>Returns the number of registered providers.</summary>
    int Count { get; }
}
