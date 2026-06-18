using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.Guids;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>The owner-scoped endpoint for reporting (flagging) a message for review.</summary>
internal static class MessageReportEndpoints
{
    internal static RouteGroupBuilder MapMessageReportEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/messages/{messageId:guid}/report", ReportAsync)
            .WithName("ReportChatMessage")
            .WithSummary("Reports a message in one of the current user's conversations.")
            .WithDescription(
                "Records a user report flagging the message for review and raises a domain event the "
                + "application can route. Scoped to the caller: a message outside the caller's own "
                + "conversations is reported as not found. The report stores identifiers plus the "
                + "user-entered reason — never the message content (ADR-071).")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIChatPermissions.Conversations.Report);

        return group;
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> ReportAsync(
        Guid messageId,
        ReportMessageRequest request,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return TypedResults.Problem(
                detail: "The current identity has no user context.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        bool reported = await store.ReportMessageAsync(
            guidGenerator.Create(), messageId, ownerId, request.Reason, request.Category, cancellationToken)
            .ConfigureAwait(false);

        return reported
            ? TypedResults.Accepted((string?)null)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}
