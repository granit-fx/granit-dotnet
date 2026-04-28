using Granit.Authentication.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.OpenIddict.Extensions;

/// <summary>
/// OpenIddict-specific sugar over <c>AddGranitRoleClaimNormalization</c>.
/// </summary>
public static class OpenIddictRoleClaimNormalizationServiceCollectionExtensions
{
    /// <summary>
    /// The authentication scheme name registered by <c>OpenIddict.Validation.AspNetCore</c>.
    /// Hardcoded as a string literal (rather than referencing
    /// <c>OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme</c>) so this
    /// helper does not force <c>Granit.Authentication.OpenIddict</c> consumers to
    /// transitively depend on the OpenIddict ASP.NET Core integration assembly when
    /// they only want the role-claim contract restored.
    /// </summary>
    public const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";

    /// <summary>
    /// Registers role-claim normalization for the
    /// <c>OpenIddict.Validation.AspNetCore</c> authentication scheme: copies the OIDC
    /// short-name <c>role</c> claim emitted by OpenIddict.Validation onto
    /// <see cref="System.Security.Claims.ClaimTypes.Role"/>, restoring parity with
    /// what <c>Granit.Authentication.JwtBearer</c> achieves implicitly via
    /// <c>TokenValidationParameters.RoleClaimType</c>. Without this,
    /// <c>ICurrentUserService.GetRoles()</c> and
    /// <c>Granit.Authorization.PermissionChecker</c>'s <c>AdminRoles</c> bypass
    /// disagree with <see cref="System.Security.Claims.ClaimsPrincipal.IsInRole(string)"/>
    /// — IsInRole matches, GetRoles returns empty, and admin users get 403.
    /// </summary>
    public static IServiceCollection AddGranitOpenIddictRoleClaimNormalization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddGranitRoleClaimNormalization(o =>
        {
            if (!o.Schemes.Contains(OpenIddictValidationScheme))
            {
                o.Schemes.Add(OpenIddictValidationScheme);
            }
        });
    }
}
