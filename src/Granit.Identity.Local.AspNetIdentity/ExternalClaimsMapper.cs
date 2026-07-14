using System.Security.Claims;

namespace Granit.Identity.Local.AspNetIdentity;

/// <summary>
/// Maps claims from an external identity provider to user properties.
/// </summary>
/// <remarks>
/// This class is <see langword="virtual"/> — override in your host application
/// for custom claim mapping by registering a subclass before calling
/// <c>AddGranitOpenIddictClient()</c>.
/// </remarks>
public class ExternalClaimsMapper
{
    /// <summary>
    /// Extracts user properties from external provider claims.
    /// </summary>
    /// <param name="principal">The claims principal from the external provider.</param>
    /// <param name="provider">The provider name (e.g., "Google").</param>
    /// <returns>The mapped user properties.</returns>
    public virtual ExternalUserProperties MapToUserProperties(ClaimsPrincipal principal, string provider)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new ExternalUserProperties
        {
            Email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst("email")?.Value,
            FirstName = principal.FindFirst(ClaimTypes.GivenName)?.Value
                        ?? principal.FindFirst("given_name")?.Value,
            LastName = principal.FindFirst(ClaimTypes.Surname)?.Value
                       ?? principal.FindFirst("family_name")?.Value,
            UserName = principal.FindFirst(ClaimTypes.Email)?.Value
                       ?? principal.FindFirst("email")?.Value,
        };
    }
}

/// <summary>
/// User properties extracted from external provider claims.
/// </summary>
public sealed class ExternalUserProperties
{
    /// <summary>Gets or sets the email address.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the username.</summary>
    public string? UserName { get; set; }
}
