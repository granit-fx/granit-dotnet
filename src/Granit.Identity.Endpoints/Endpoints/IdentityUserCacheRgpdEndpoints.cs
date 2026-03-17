using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// RGPD endpoints for erasing or pseudonymizing cached user data.
/// </summary>
internal static class IdentityUserCacheRgpdEndpoints
{
    internal static RouteGroupBuilder MapRgpdEndpoints(this RouteGroupBuilder group)
    {
        group.MapDelete("/{userId}/erase", EraseAsync)
            .WithName("EraseIdentityUserCache")
            .WithSummary("RGPD Art. 17 — permanently deletes the cached entry for a user.")
            .WithDescription("Implements the RGPD right to erasure (Article 17). Permanently removes the user's cached identity data. This does not affect the upstream identity provider — the data will be re-cached if the user is looked up again. For full erasure, also delete in the identity provider.");

        group.MapPost("/{userId}/pseudonymize", PseudonymizeAsync)
            .WithName("PseudonymizeIdentityUserCache")
            .WithSummary("RGPD Art. 18 — replaces PII with anonymized data in the cached entry.")
            .WithDescription("Implements the RGPD right to restriction of processing (Article 18). Replaces all personally identifiable information (name, email, etc.) with anonymized placeholders while preserving the entry for referential integrity. Irreversible.");

        return group;
    }

    private static async Task<NoContent> EraseAsync(
        string userId,
        IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        await lookupService.DeleteByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> PseudonymizeAsync(
        string userId,
        IUserLookupService lookupService,
        CancellationToken cancellationToken)
    {
        await lookupService.PseudonymizeByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
