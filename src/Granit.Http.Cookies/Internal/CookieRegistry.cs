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
            CookieDefinition existing = _cookies[definition.Name];

            // Idempotent: same definition re-registered (hot-reload, module restart) — no-op.
            if (existing == definition)
            {
                return;
            }

            // Different definition with same name — genuine conflict between modules.
            throw new InvalidOperationException(
                $"Cookie '{definition.Name}' is already registered with a different definition.");
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
