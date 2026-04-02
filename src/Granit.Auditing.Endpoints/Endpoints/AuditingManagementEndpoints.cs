using Granit.Auditing.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Auditing.Endpoints.Endpoints;

/// <summary>
/// Management Minimal API endpoints for the audit trail (GDPR operations).
/// </summary>
internal static class AuditingManagementEndpoints
{
    /// <summary>Maps audit log management endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapAuditingManagementEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/pseudonymize/{userId}", PseudonymizeAsync)
            .WithName("PseudonymizeAuditEntries")
            .WithSummary("Pseudonymize all audit entries for a specific user.")
            .WithDescription(
                "Replaces personal data (UserId, UserName, IpAddress, UserAgent) in all audit entries " +
                "belonging to the specified user with pseudonymized values (GDPR Art. 17). " +
                "The UserId is replaced with a SHA-256 hash to preserve audit trail correlation " +
                "without re-identification. Audit entry integrity is maintained per ISO 27001 A.12.4. " +
                "Returns 204 No Content on success.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuditingPermissions.AuditEntries.Manage);

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PseudonymizeAsync(
        string userId,
        [FromServices] IAuditingCleaner cleaner,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return TypedResults.Problem(
                detail: "The userId path parameter must not be empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await cleaner.PseudonymizeByUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
