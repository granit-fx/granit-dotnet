namespace Granit.Http.Cookies;

/// <summary>
/// Registry of third-party services that set cookies on the client.
/// Services are declared in configuration and consumed by the cookie consent endpoint.
/// </summary>
public interface IThirdPartyServiceRegistry
{
    /// <summary>Returns all registered third-party service definitions.</summary>
    IReadOnlyList<ThirdPartyServiceDefinition> GetAll();

    /// <summary>Returns all services matching the specified category.</summary>
    IReadOnlyList<ThirdPartyServiceDefinition> GetByCategory(CookieCategory category);
}
