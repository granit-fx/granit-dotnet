namespace Granit.Http.Cookies;

/// <summary>
/// Registry of all declared cookies.
/// Cookies must be registered at startup; unregistered cookies are rejected at runtime (Fail-Fast).
/// </summary>
public interface ICookieRegistry
{
    /// <summary>Registers a cookie definition. Throws if the name is already registered.</summary>
    void Register(CookieDefinition definition);

    /// <summary>Returns the definition for the given cookie name, or <c>null</c> if not registered.</summary>
    CookieDefinition? GetDefinition(string cookieName);

    /// <summary>Returns all definitions matching the specified category.</summary>
    IReadOnlyList<CookieDefinition> GetByCategory(CookieCategory category);

    /// <summary>Returns <c>true</c> if a cookie with the given name is registered.</summary>
    bool IsRegistered(string cookieName);

    /// <summary>Returns all registered cookie definitions.</summary>
    IReadOnlyList<CookieDefinition> GetAll();
}
