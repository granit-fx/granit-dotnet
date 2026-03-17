namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Immutable registry of third-party service definitions.
/// Populated at startup from configuration.
/// </summary>
internal sealed class ThirdPartyServiceRegistry(
    IReadOnlyList<ThirdPartyServiceDefinition> services) : IThirdPartyServiceRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<ThirdPartyServiceDefinition> GetAll() => services;

    /// <inheritdoc/>
    public IReadOnlyList<ThirdPartyServiceDefinition> GetByCategory(CookieCategory category) =>
        services.Where(s => s.Category == category).ToList();
}
