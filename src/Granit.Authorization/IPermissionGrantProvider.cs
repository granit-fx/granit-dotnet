namespace Granit.Authorization;

/// <summary>
/// Extracts the grantee identifiers that the permission checker should consult for a given
/// principal. Registered via <c>TryAddEnumerable</c>; evaluated by <c>PermissionChecker</c>
/// in the order they are registered (specific → generic: User, Role, Client).
/// </summary>
/// <remarks>
/// Providers are deliberately read-only projections of a <see cref="PermissionGrantLookupContext"/>.
/// They do not execute grant lookups themselves; the shared <see cref="IPermissionGrantStore"/>
/// handles persistence and the shared cache handles memoization. A custom provider only needs
/// to decide which keys to look up (role names, org ids, geography codes, etc.).
/// </remarks>
public interface IPermissionGrantProvider
{
    /// <summary>
    /// Provider identifier written into <see cref="Domain.PermissionGrant.ProviderName"/>.
    /// Use <see cref="PermissionGrantProviderNames"/> constants for the built-in providers.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns the provider keys to consult for the current principal. An empty list means
    /// the provider has nothing to contribute (e.g. anonymous user for the user provider).
    /// Multiple keys (typical for roles) are queried one-by-one until any grants the permission.
    /// </summary>
    IReadOnlyList<string> GetProviderKeys(PermissionGrantLookupContext context);
}
