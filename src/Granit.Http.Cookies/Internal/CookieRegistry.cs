using System.Collections.Concurrent;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Thread-safe singleton registry of cookie definitions.
/// </summary>
internal sealed class CookieRegistry : ICookieRegistry
{
    private readonly ConcurrentDictionary<string, CookieDefinition> _cookies = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(CookieDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_cookies.TryAdd(definition.Name, definition))
        {
            // Idempotent: allow re-registration of the same cookie (hot-reload, module restart).
            _cookies[definition.Name] = definition;
        }
    }

    /// <inheritdoc/>
    public CookieDefinition? GetDefinition(string cookieName) =>
        _cookies.GetValueOrDefault(cookieName);

    /// <inheritdoc/>
    public IReadOnlyList<CookieDefinition> GetByCategory(CookieCategory category) =>
        _cookies.Values.Where(c => c.Category == category).ToList();

    /// <inheritdoc/>
    public bool IsRegistered(string cookieName) =>
        _cookies.ContainsKey(cookieName);

    /// <inheritdoc/>
    public IReadOnlyList<CookieDefinition> GetAll() =>
        _cookies.Values.ToList();
}
