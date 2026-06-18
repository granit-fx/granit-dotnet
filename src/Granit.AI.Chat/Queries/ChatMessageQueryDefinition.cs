using Granit.AI.Chat.Domain;
using Granit.QueryEngine;

namespace Granit.AI.Chat.Queries;

/// <summary>
/// Query definition for a conversation's <see cref="Message"/> thread, used by the
/// <c>GET /conversations/{id}/messages</c> endpoint to page <strong>backwards</strong> through
/// history: newest messages first, then older pages on scroll-up.
/// </summary>
/// <remarks>
/// <para>
/// Keyset (cursor) pagination over the composite <c>createdAt</c> + <c>id</c> key gives stable
/// pages even as new messages arrive at the head — there is no offset to drift. The default sort
/// is <c>-createdAt</c> (newest first); the unique message <c>Id</c> is the tiebreaker, so
/// the cursor walks strictly toward older messages and dries up (<c>NextCursor == null</c>) at the
/// start of history.
/// </para>
/// <para>
/// This definition is driven directly via <see cref="IQueryEngine{TEntity}"/> by the bespoke,
/// owner-scoped messages endpoint (the source is pre-scoped to one conversation), not exposed
/// through <c>MapGranitQuery</c>; the columns exist only to whitelist the sort/cursor keys, so they
/// carry no localization metadata.
/// </para>
/// </remarks>
public sealed class ChatMessageQueryDefinition : QueryDefinition<Message>
{
    /// <inheritdoc/>
    public override string Name => "Granit.AI.Chat.ChatMessageQuery";

    /// <inheritdoc/>
    protected override void Configure(QueryDefinitionBuilder<Message> builder) =>
        builder
            .Column(m => m.CreatedAt, c => c.Label("Created At").Sortable())
            .Column(m => m.Id, c => c.Label("Id").Sortable())
            .SupportsCursorPagination(m => m.Id)
            .DefaultSort("-createdAt")
            .DefaultPageSize(30)
            .MaxPageSize(100);
}
