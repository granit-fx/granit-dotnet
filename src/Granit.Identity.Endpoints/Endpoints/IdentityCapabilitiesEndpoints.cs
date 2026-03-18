using Granit.Identity.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Endpoint exposing the active identity provider's capabilities.
/// </summary>
internal static class IdentityCapabilitiesEndpoints
{
    internal static RouteGroupBuilder MapCapabilitiesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/capabilities", GetCapabilities)
            .WithName("GetIdentityProviderCapabilities")
            .WithSummary("Returns the capabilities of the active identity provider.")
            .WithDescription("Returns the feature flags of the currently active identity provider (Keycloak, Azure AD, etc.): session termination support, native password reset, group hierarchy, custom attributes, credential verification, and user creation. The front-end uses this to conditionally render identity management features.");

        return group;
    }

    private static Ok<IdentityProviderCapabilitiesResponse> GetCapabilities(
        [FromServices] IIdentityProviderCapabilities capabilities) =>
        TypedResults.Ok(new IdentityProviderCapabilitiesResponse(
            capabilities.ProviderName,
            capabilities.SupportsIndividualSessionTermination,
            capabilities.SupportsNativePasswordResetEmail,
            capabilities.SupportsGroupHierarchy,
            capabilities.SupportsCustomAttributes,
            capabilities.MaxCustomAttributes,
            capabilities.SupportsCredentialVerification,
            capabilities.SupportsUserCreation));
}
