using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Regulations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyRegulationEndpoints
{
    internal static RouteGroupBuilder MapPrivacyRegulationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/regulation", HandleGetRegulationAsync)
             .WithName("GetApplicableRegulation")
             .WithSummary("Returns the effective privacy regulation profile for the current tenant.")
             .WithDescription(
                 "Resolves the effective privacy regulation for the current tenant via IPrivacyRegulationResolver. "
                 + "For multi-jurisdiction tenants, returns a composite profile (most restrictive rules win) "
                 + "with contributingRegulations listing the source regulations. "
                 + "Single-jurisdiction response is backward-compatible: contributingRegulations contains one entry.")
             .Produces<PrivacyRegulationProfileResponse>();

        return group;
    }

    private static async Task<Ok<PrivacyRegulationProfileResponse>> HandleGetRegulationAsync(
        [FromServices] IPrivacyRegulationResolver resolver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PrivacyRegulationProfile> contributing =
            await resolver.ResolveAllAsync(cancellationToken).ConfigureAwait(false);

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PrivacyRegulationProfileResponse(
            profile.Regulation.Value,
            profile.DisplayName,
            profile.JurisdictionCode,
            contributing.Select(p => p.Regulation.Value).ToList(),
            profile.ConsentModel.ToString(),
            profile.AvailableLegalBases.Select(b => b.Value).ToList(),
            profile.SubjectAccessRequestDays,
            profile.SubjectAccessRequestExtensionDays,
            profile.DeletionRequestDays,
            profile.DefaultDeletionGracePeriodDays,
            profile.MaxDeletionGracePeriodDays,
            profile.BreachNotifyAuthorityHours,
            profile.BreachNotifyIndividualsHours,
            profile.MinimumConsentAge,
            profile.RequiresParentalIdentityVerification,
            profile.CookieConsentModel.ToString(),
            profile.HonorGlobalPrivacyControl,
            profile.RequiresCrossBorderAssessment,
            profile.TransferMechanisms.ToList(),
            profile.DataLocalizationRequired,
            profile.RequiresDpoOrRepresentative,
            profile.RequiredExportFormats.ToList()));
    }
}
