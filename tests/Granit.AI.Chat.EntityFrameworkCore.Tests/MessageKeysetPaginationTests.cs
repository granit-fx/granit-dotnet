using Granit.AI.Chat.Domain;
using Granit.AI.Chat.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Shouldly;

namespace Granit.AI.Chat.EntityFrameworkCore.Tests;

/// <summary>
/// Exercises <see cref="EfConversationStore.GetMessagesPageAsync"/> against the real EF Core query
/// engine (SQLite) to prove the backwards keyset behaviour the messages endpoint depends on: newest
/// page first, a cursor that walks strictly toward older messages, a null cursor at the start of
/// history, owner scoping, and conversation scoping.
/// </summary>
public sealed class MessageKeysetPaginationTests : IAsyncLifetime
{
    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly Guid _otherConversationId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private TestDbContextFactory _factory = null!;
    private EfConversationStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = TestDbContextFactory.Create();

        var baseTime = new DateTimeOffset(2026, 6, 18, 8, 0, 0, TimeSpan.Zero);
        await using (AIChatDbContext seed = _factory.CreateDbContext())
        {
            // Parent conversations must exist to satisfy the message → conversation foreign key.
            seed.Conversations.Add(Conversation.Create(_conversationId, _ownerId, "target"));
            seed.Conversations.Add(Conversation.Create(_otherConversationId, _ownerId, "other"));

            // Five messages in the target conversation, oldest (m0) → newest (m4).
            for (int i = 0; i < 5; i++)
            {
                var message = Message.Create(Guid.NewGuid(), _conversationId, MessageRole.User, $"m{i}");
                message.CreatedAt = baseTime.AddMinutes(i);
                seed.Set<Message>().Add(message);
            }

            // A message in another conversation that must never leak into the scoped page.
            var foreign = Message.Create(Guid.NewGuid(), _otherConversationId, MessageRole.User, "foreign");
            foreign.CreatedAt = baseTime.AddMinutes(2);
            seed.Set<Message>().Add(foreign);

            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _store = new EfConversationStore(_factory, TestQueryEngine.ForMessages());
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<PagedResult<Message>?> PageAsync(string? cursor, int pageSize) =>
        _store.GetMessagesPageAsync(_conversationId, _ownerId, cursor, pageSize, TestContext.Current.CancellationToken);

    [Fact]
    public async Task First_page_returns_the_newest_messages_with_a_next_cursor()
    {
        PagedResult<Message>? page = await PageAsync(cursor: null, pageSize: 2);

        page.ShouldNotBeNull();
        page.Items.Select(m => m.Content).ShouldBe(["m4", "m3"]);
        page.TotalCount.ShouldBeNull();
        page.HasMore.ShouldBeTrue();
        page.NextCursor.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cursor_walks_toward_older_messages_then_dries_up_at_history_start()
    {
        List<string> seen = [];
        string? cursor = null;
        PagedResult<Message>? page;

        do
        {
            page = await PageAsync(cursor, pageSize: 2);
            page.ShouldNotBeNull();
            seen.AddRange(page.Items.Select(m => m.Content));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        // Strictly newest → oldest, every message exactly once, scoped to the conversation.
        seen.ShouldBe(["m4", "m3", "m2", "m1", "m0"]);
        seen.ShouldNotContain("foreign");
        page.HasMore.ShouldBeFalse();
        page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task Returns_null_for_a_conversation_that_is_not_the_owners()
    {
        PagedResult<Message>? page = await _store.GetMessagesPageAsync(
            _conversationId, ownerId: Guid.NewGuid(), cursor: null, pageSize: 2, TestContext.Current.CancellationToken);

        page.ShouldBeNull();
    }
}
