using System.Security.Claims;

namespace Granit.OpenIddict.Services;

/// <summary>
/// Determines which tokens (access_token, id_token) each claim should be included in.
/// </summary>
/// <remarks>
/// <para>
/// OpenIddict requires every claim on the <see cref="ClaimsPrincipal"/> to have at least
/// one destination, otherwise it is silently excluded from all tokens.
/// </para>
/// <para>
/// Register a custom implementation to control claim visibility:
/// <code>
/// services.Replace(ServiceDescriptor.Scoped&lt;IClaimsDestinationProvider, MyProvider&gt;());
/// </code>
/// </para>
/// </remarks>
public interface IClaimsDestinationProvider
{
    /// <summary>
    /// Returns the destinations for the given claim.
    /// </summary>
    /// <param name="claim">The claim to route.</param>
    /// <param name="principal">The full claims principal (for context-aware decisions).</param>
    /// <returns>
    /// One or more destination constants from <see cref="ClaimsDestinations"/>.
    /// Return an empty collection to exclude the claim from all tokens.
    /// </returns>
    IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal);
}

/// <summary>
/// Well-known token destination constants.
/// </summary>
#pragma warning disable GRSEC003 // Token destination constants, not secrets
public static class ClaimsDestinations
{
    /// <summary>Include the claim in the access token.</summary>
    public const string AccessToken = "access_token";

    /// <summary>Include the claim in the identity token.</summary>
    public const string IdentityToken = "id_token";
}
#pragma warning restore GRSEC003
