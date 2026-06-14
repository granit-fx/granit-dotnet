using Granit.AI.Chat.Domain;
using Granit.AI.Chat.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Granit.AI.Chat.EntityFrameworkCore.Tests;

public sealed class EfConversationDataManagerTests : IDisposable
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfConversationDataManager _sut;

    public EfConversationDataManagerTests() => _sut = new EfConversationDataManager(_factory);

    public void Dispose() => _factory.Dispose();

    private async Task<Conversation> SeedAsync(Guid owner, DateTimeOffset createdAt, params string[] messages)
    {
        var conversation = Conversation.Create(Guid.NewGuid(), owner, "title");
        conversation.CreatedAt = createdAt;
        foreach (string text in messages)
        {
            Message message = conversation.AddMessage(Guid.NewGuid(), MessageRole.User, text);
            message.CreatedAt = createdAt;
        }

        await using AIChatDbContext context = _factory.CreateDbContext();
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return conversation;
    }

    private async Task<int> CountConversationsAsync()
    {
        await using AIChatDbContext context = _factory.CreateDbContext();
        return await context.Conversations.IgnoreQueryFilters()
            .CountAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAllForOwner_returns_the_owners_conversations_with_messages()
    {
        await SeedAsync(UserA, When(2024), "hi", "there");
        await SeedAsync(UserA, When(2024), "second");
        await SeedAsync(UserB, When(2024), "other");

        IReadOnlyList<Conversation> result = await _sut.GetAllForOwnerAsync(UserA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.SelectMany(c => c.Messages).Count().ShouldBe(3);
    }

    [Fact]
    public async Task EraseOwner_hard_deletes_only_that_owners_conversations_and_messages()
    {
        await SeedAsync(UserA, When(2024), "a1", "a2");
        await SeedAsync(UserB, When(2024), "b1");

        int erased = await _sut.EraseOwnerAsync(tenantId: null, UserA, TestContext.Current.CancellationToken);

        erased.ShouldBe(1);
        (await CountConversationsAsync()).ShouldBe(1); // UserB survives
        (await _sut.GetAllForOwnerAsync(UserA, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task EraseOwner_is_idempotent()
    {
        await SeedAsync(UserA, When(2024), "a1");

        (await _sut.EraseOwnerAsync(null, UserA, TestContext.Current.CancellationToken)).ShouldBe(1);
        (await _sut.EraseOwnerAsync(null, UserA, TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task PurgeOlderThan_removes_conversations_past_the_cutoff_and_keeps_recent_ones()
    {
        await SeedAsync(UserA, When(2020), "old");
        await SeedAsync(UserA, When(2025), "recent");

        int purged = await _sut.PurgeOlderThanAsync(When(2023), batchSize: 100, TestContext.Current.CancellationToken);

        purged.ShouldBe(1);
        IReadOnlyList<Conversation> remaining = await _sut.GetAllForOwnerAsync(UserA, TestContext.Current.CancellationToken);
        remaining.ShouldHaveSingleItem().Messages.ShouldContain(m => m.Content == "recent");
    }

    [Fact]
    public async Task PurgeOlderThan_uses_last_message_activity_not_creation()
    {
        // Conversation created long ago but with a recent message must survive.
        var conversation = Conversation.Create(Guid.NewGuid(), UserA, "title");
        conversation.CreatedAt = When(2020);
        Message recent = conversation.AddMessage(Guid.NewGuid(), MessageRole.User, "still active");
        recent.CreatedAt = When(2025);
        await using (AIChatDbContext context = _factory.CreateDbContext())
        {
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int purged = await _sut.PurgeOlderThanAsync(When(2023), 100, TestContext.Current.CancellationToken);

        purged.ShouldBe(0);
    }

    private static DateTimeOffset When(int year) => new(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
