using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Granit.Identity.Local.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> related to impersonation.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Claim type for the impersonating administrator's user ID.</summary>
    public const string ImpersonatorIdClaimType = "impersonator_id";

    /// <summary>Claim type for the impersonating administrator's display name.</summary>
    public const string ImpersonatorNameClaimType = "impersonator_name";

    /// <summary>
    /// Returns <see langword="true"/> if the current session is an impersonation session.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns><see langword="true"/> if an <c>impersonator_id</c> claim is present.</returns>
    public static bool IsImpersonated(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ImpersonatorIdClaimType) is not null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the current HTTP request is an impersonation session.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns><see langword="true"/> if an <c>impersonator_id</c> claim is present.</returns>
    public static bool IsImpersonated(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.User.IsImpersonated();
    }

    /// <summary>
    /// Returns the impersonator's user ID, or <see langword="null"/> if not impersonating.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The impersonator's user ID string, or <see langword="null"/>.</returns>
    public static string? FindImpersonatorUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ImpersonatorIdClaimType)?.Value;
    }

    /// <summary>
    /// Returns the impersonator's display name, or <see langword="null"/> if not impersonating.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The impersonator's name, or <see langword="null"/>.</returns>
    public static string? FindImpersonatorName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(ImpersonatorNameClaimType)?.Value;
    }
}
