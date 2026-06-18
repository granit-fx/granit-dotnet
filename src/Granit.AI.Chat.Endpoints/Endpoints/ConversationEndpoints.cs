using Granit.AI.Chat.Domain;
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

/// <summary>Owner-scoped CRUD endpoints for chat conversations.</summary>
internal static class ConversationEndpoints
{
    internal static RouteGroupBuilder MapConversationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListConversations")
            .WithSummary("Lists the current user's conversations.")
            .WithDescription("Returns the caller's own conversations, newest first, without their messages.")
            .Produces<IReadOnlyList<ConversationSummaryResponse>>()
            .RequireAuthorization(AIChatPermissions.Conversations.Read);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetConversation")
            .WithSummary("Returns one of the current user's conversations by ID.")
            .WithDescription("Returns the conversation and its messages. Scoped to the caller: another user's conversation is reported as not found.")
            .Produces<ConversationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIChatPermissions.Conversations.Read);

        group.MapPost("/", CreateAsync)
            .WithName("CreateConversation")
            .WithSummary("Creates a conversation owned by the current user.")
            .WithDescription("Creates an empty conversation with the given title, owned by the caller.")
            .Produces<ConversationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(AIChatPermissions.Conversations.Manage);

        group.MapPut("/{id:guid}/title", RenameAsync)
            .WithName("RenameConversation")
            .WithSummary("Renames one of the current user's conversations.")
            .WithDescription("Updates the conversation title. Scoped to the caller: another user's conversation is reported as not found.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIChatPermissions.Conversations.Manage);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteConversation")
            .WithSummary("Deletes one of the current user's conversations.")
            .WithDescription("Soft-deletes the conversation. Scoped to the caller: another user's conversation is reported as not found.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIChatPermissions.Conversations.Delete);

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<ConversationSummaryResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        IReadOnlyList<Conversation> conversations = await store.ListAsync(ownerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ConversationSummaryResponse> response =
            [.. conversations.Select(ConversationSummaryResponse.FromAggregate)];
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ConversationResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        Conversation? conversation = await store.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return conversation is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(ConversationResponse.FromAggregate(conversation));
    }

    private static async Task<Results<Created<ConversationResponse>, ProblemHttpResult>> CreateAsync(
        CreateConversationRequest request,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        var conversation = Conversation.Create(guidGenerator.Create(), ownerId, request.Title);
        await store.CreateAsync(conversation, cancellationToken).ConfigureAwait(false);

        var response = ConversationResponse.FromAggregate(conversation);
        return TypedResults.Created($"/conversations/{conversation.Id}", response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RenameAsync(
        Guid id,
        RenameConversationRequest request,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        bool renamed = await store.RenameAsync(id, ownerId, request.Title, cancellationToken).ConfigureAwait(false);
        return renamed
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        bool deleted = await store.DeleteAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }

    private static ProblemHttpResult Unauthorized() =>
        TypedResults.Problem(
            detail: "The current identity has no user context.",
            statusCode: StatusCodes.Status401Unauthorized);
}
