using System.Diagnostics;
using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountDeletionEndpoints
{
    internal static RouteGroupBuilder MapAccountDeletionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/delete", DeleteAccountAsync)
            .WithName("DeleteAccount")
            .WithSummary("Deletes the authenticated user's account.")
            .WithDescription(
                "Initiates account deletion (GDPR Article 17 — right to erasure). "
                + "Requires password confirmation. Triggers soft-delete, token revocation, "
                + "and publishes AccountDeletedEto for downstream cleanup. Returns 202 "
                + "as deletion is asynchronous.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization();

        return group;
    }

    private static async Task<Results<Accepted<string>, ProblemHttpResult>> DeleteAccountAsync(
        AccountDeleteRequest request,
        HttpContext httpContext,
[FromServices] Granit.Identity.IIdentityCredentialVerifier credentialVerifier,
[FromServices] IAccountDeletionService deletionService,
        CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.AccountDeletion);

        string userId = httpContext.User.FindFirst("sub")!.Value;
        string? username = httpContext.User.FindFirst("preferred_username")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value;

        if (username is null)
        {
            return TypedResults.Problem(
                detail: "Unable to determine username from token claims.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Verify password before deletion
        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: "Password is incorrect.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await deletionService.InitiateAsync(userId, cancellationToken).ConfigureAwait(false);

        string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        httpContext.RequestServices.GetService<IdentityLocalMetrics>()?.RecordAccountDeletion(tenantId);
        return TypedResults.Accepted((string?)null, (string?)null);
    }
}
