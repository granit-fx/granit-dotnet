using System.Security.Claims;
using Granit.OpenIddict.Services;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Extensions;

/// <summary>
/// Extension methods to apply claim destinations on a <see cref="ClaimsPrincipal"/>
/// before issuing tokens via OpenIddict.
/// </summary>
public static class ClaimsPrincipalDestinationExtensions
{
    /// <summary>
    /// Sets the destinations for all claims on the principal using the provided
    /// <see cref="IClaimsDestinationProvider"/>.
    /// </summary>
    /// <remarks>
    /// Call this on the <see cref="ClaimsPrincipal"/> before returning it from
    /// the authorization or token endpoint handler. OpenIddict requires destinations
    /// to be set on every claim — claims without destinations are excluded.
    /// </remarks>
    /// <param name="principal">The claims principal to annotate.</param>
    /// <param name="destinationProvider">The destination provider.</param>
    /// <returns>The same principal for chaining.</returns>
    public static ClaimsPrincipal SetDestinations(
        this ClaimsPrincipal principal,
        IClaimsDestinationProvider destinationProvider)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(destinationProvider);

        foreach (Claim claim in principal.Claims)
        {
            claim.SetDestinations(destinationProvider.GetDestinations(claim, principal));
        }

        return principal;
    }
}
