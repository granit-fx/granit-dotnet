using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

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
        ValidateHostPrefixCompliance(definition);

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

    /// <summary>
    /// Validates that cookies with the <c>__Host-</c> prefix comply with RFC 6265bis §4.1.3.2:
    /// Secure, Path="/", no Domain attribute (enforced by not setting Domain).
    /// </summary>
    private static void ValidateHostPrefixCompliance(CookieDefinition definition)
    {
        if (!definition.Name.StartsWith("__Host-", StringComparison.Ordinal))
        {
            return;
        }

        if (definition.Path != "/")
        {
            throw new InvalidOperationException(
                $"Cookie '{definition.Name}' uses the __Host- prefix but Path is '{definition.Path}' (must be '/').");
        }

        if (definition.SameSite == SameSiteMode.None && !definition.IsHttpOnly)
        {
            throw new InvalidOperationException(
                $"Cookie '{definition.Name}' uses the __Host- prefix but SameSite=None without HttpOnly is insecure.");
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
