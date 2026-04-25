namespace Granit.Identity;

/// <summary>
/// Unified abstraction for a user across all identity providers.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by:
/// <list type="bullet">
/// <item><c>FederatedIdentityUser</c> — immutable snapshot from external providers (Keycloak, EntraID, etc.)</item>
/// <item><c>GranitUser</c> — EF Core entity for local mode (OpenIddict / ASP.NET Core Identity)</item>
/// <item><c>CachedIdentityUser</c> — EF Core cache entry for external providers</item>
/// </list>
/// </para>
/// <para>
/// All framework consumers (<c>IUserLookupService</c>, <c>IIdentityUserReader</c>, etc.)
/// return <see cref="IIdentityUser"/> — the concrete type depends on which provider is active.
/// </para>
/// </remarks>
public interface IIdentityUser
{
    /// <summary>
    /// User identifier (string for all modes).
    /// </summary>
    /// <remarks>
    /// For local mode (<c>GranitUser</c>), returns <c>Id.ToString()</c>.
    /// For external mode, returns the provider's native identifier (e.g., Keycloak subject).
    /// </remarks>
    string UserId { get; }

    /// <summary>Login name.</summary>
    string? Username { get; }

    /// <summary>Email address.</summary>
    string? Email { get; }

    /// <summary>First name.</summary>
    string? FirstName { get; }

    /// <summary>Last name.</summary>
    string? LastName { get; }

    /// <summary>Whether the user account is active.</summary>
    bool Enabled { get; }

    /// <summary>
    /// Extensible property bag for provider-specific or application-specific data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Never <see langword="null"/> — returns an empty dictionary when no extra properties are set.
    /// </para>
    /// <para>
    /// For <c>GranitUser</c>: deserialized from <c>CustomAttributesJson</c> (JSONB column).
    /// For Keycloak: flattened user attributes.
    /// For <c>CachedIdentityUser</c>: deserialized from <c>MetadataJson</c>.
    /// </para>
    /// <para>
    /// Applications can define typed accessors via extension methods:
    /// <code>
    /// public static string? GetDepartment(this IIdentityUser user)
    ///     => user.Metadata.GetValueOrDefault("Department");
    /// </code>
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
