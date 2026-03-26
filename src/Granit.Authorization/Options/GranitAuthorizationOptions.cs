using System.ComponentModel.DataAnnotations;

namespace Granit.Authorization.Options;

/// <summary>Configuration options for the Granit.Authorization module.</summary>
public sealed class GranitAuthorizationOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Authorization";

    /// <summary>
    /// Roles that bypass all permission checks. These roles are the root of trust
    /// and cannot be restricted via <c>IPermissionManagerWriter.SetAsync()</c>.
    /// Defaults to <c>["admin"]</c>. Comparison is case-insensitive.
    /// </summary>
    [MinLength(1)]
    public IList<string> AdminRoles { get; set; } = ["admin"];

    /// <summary>
    /// Duration for which permission check results are cached per (TenantId, RoleName, PermissionName).
    /// Defaults to 5 minutes. Must be between 10 seconds and 30 minutes.
    /// Should be less than or equal to the JWT token lifetime.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "00:30:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When true, all permission checks return granted for authenticated users.
    /// For development and testing only — never enable in production.
    /// Anonymous users are always denied regardless of this setting.
    /// </summary>
    public bool AlwaysAllow { get; set; }
}
