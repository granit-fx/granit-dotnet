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
             .WithSummary("Returns the privacy regulation profile applicable to the current tenant.")
             .WithDescription(
                 "Resolves the privacy regulation for the current tenant context via IPrivacyRegulationResolver. "
                 + "Returns the full regulation profile including consent model, response timelines, breach notification "
                 + "deadlines, age thresholds, cookie consent rules, and cross-border transfer requirements.")
             .Produces<PrivacyRegulationProfileResponse>();

        return group;
    }

    private static async Task<Ok<PrivacyRegulationProfileResponse>> HandleGetRegulationAsync(
        [FromServices] IPrivacyRegulationResolver resolver,
        CancellationToken cancellationToken)
    {
        PrivacyRegulationProfile profile = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PrivacyRegulationProfileResponse(
            profile.Regulation.Value,
            profile.DisplayName,
            profile.JurisdictionCode,
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
