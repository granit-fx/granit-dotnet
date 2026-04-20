using Granit.Authorization.Extensions;
using Granit.Tax.Endpoints.Dtos;
using Granit.Tax.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Tax.Endpoints.Endpoints;

/// <summary>Endpoints for tax ID validation.</summary>
internal static class TaxValidationEndpoints
{
    internal static RouteGroupBuilder MapValidationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/validate", ValidateAsync)
            .RequireAuthorization(TaxPermissions.Validations.Execute)
            .AllowHostAccess()
            .WithName("ValidateTaxId")
            .WithSummary("Validates a tax ID online against the relevant tax authority.")
            .WithDescription(
                "Submits a tax identification number (e.g., EU VAT, UK VAT, US EIN) for online verification. "
                + "Uses the configured provider (VIES for EU, Stripe for other jurisdictions). "
                + "Results are cached according to the configured TTL to avoid repeated API calls.")
            .Produces<TaxValidateResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Ok<TaxValidateResponse>> ValidateAsync(
        TaxValidateRequest request,
        [FromServices] ITaxIdValidator validator,
        CancellationToken cancellationToken = default)
    {
        TaxIdValidationResult result = await validator
            .ValidateAsync(request.TaxId, request.CountryCode, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new TaxValidateResponse(
            IsValid: result.IsValid,
            CompanyName: result.CompanyName,
            CompanyAddress: result.CompanyAddress,
            RequestIdentifier: result.RequestIdentifier,
            ValidatedAt: result.ValidatedAt,
            Source: result.Source.ToString()));
    }
}
