using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.ProcessingPurposes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyPurposesEndpoints
{
    internal static RouteGroupBuilder MapPrivacyPurposesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/purposes", HandleGetPurposesAsync)
             .RequireAuthorization(PrivacyPermissions.Purposes.Read)
             .WithName("ListProcessingPurposes")
             .WithSummary("Returns all registered processing purposes with their legal bases.")
             .WithDescription(
                 "Lists all processing purposes declared at application startup via RegisterProcessingPurpose(). "
                 + "Each purpose includes its legal basis, whether explicit consent is required, and an optional data category.")
             .Produces<IReadOnlyList<PrivacyProcessingPurposeResponse>>();

        return group;
    }

    private static Task<Ok<IReadOnlyList<PrivacyProcessingPurposeResponse>>> HandleGetPurposesAsync(
        [FromServices] IProcessingPurposeRegistry purposeRegistry)
    {
        IReadOnlyList<PrivacyProcessingPurposeResponse> result = purposeRegistry.GetAll()
            .Select(p => new PrivacyProcessingPurposeResponse(
                p.PurposeId, p.DisplayName, p.Description, p.LegalBasis,
                p.RequiresExplicitConsent, p.DataCategory))
            .ToList();

        return Task.FromResult(TypedResults.Ok(result));
    }
}
