using Granit.Auditing.Endpoints.Internal;
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
    /// <remarks>
    /// Authorization is deliberately <b>stacked</b>: the parent group already requires
    /// <c>Auditing.AuditEntries.Read</c> and this endpoint adds
    /// <c>Auditing.AuditEntries.Manage</c> — ASP.NET evaluates both (AND), so the effective
    /// requirement is Read <b>and</b> Manage. Intentional: an operator who may pseudonymize
    /// must also be able to review the trail they are scrubbing.
    /// </remarks>
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
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return TypedResults.Problem(
                detail: AuditingEndpointMessages.Localize(
                    httpContext,
                    "Granit:Auditing:Endpoints:UserIdRequired",
                    "The userId path parameter must not be empty."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await cleaner.PseudonymizeByUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
