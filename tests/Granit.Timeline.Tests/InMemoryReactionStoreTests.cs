using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class InMemoryReactionStoreTests
{
    private static Reaction NewReaction(Guid entryId, Guid userId, string emoji = "❤️") =>
        Reaction.Create(
            id: Guid.NewGuid(),
            entryId: entryId,
            userId: userId,
            emoji: emoji,
            createdAt: DateTimeOffset.UtcNow,
            createdBy: userId.ToString());

    [Fact]
    public async Task Add_then_GetByEntry_returns_reaction()
    {
        InMemoryReactionStore sut = new();
        var entryId = Guid.NewGuid();
        Reaction reaction = NewReaction(entryId, Guid.NewGuid());

        await sut.AddAsync(reaction, TestContext.Current.CancellationToken);
        IReadOnlyList<Reaction> rows = await sut.GetByEntryAsync(entryId, TestContext.Current.CancellationToken);

        rows.ShouldHaveSingleItem().Id.ShouldBe(reaction.Id);
    }

    [Fact]
    public async Task Add_duplicate_throws_idempotency_violation()
    {
        InMemoryReactionStore sut = new();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.AddAsync(NewReaction(entryId, userId), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.AddAsync(NewReaction(entryId, userId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remove_then_re_add_works()
    {
        InMemoryReactionStore sut = new();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.AddAsync(NewReaction(entryId, userId), TestContext.Current.CancellationToken);

        await sut.RemoveAsync(entryId, userId, "❤️", TestContext.Current.CancellationToken);
        await sut.AddAsync(NewReaction(entryId, userId), TestContext.Current.CancellationToken);

        IReadOnlyList<Reaction> rows = await sut.GetByEntryAsync(entryId, TestContext.Current.CancellationToken);
        rows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_non_existent_is_noop()
    {
        InMemoryReactionStore sut = new();
        await Should.NotThrowAsync(() =>
            sut.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), "❤️", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetByEntries_batch_filters_to_supplied_ids()
    {
        InMemoryReactionStore sut = new();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var e3 = Guid.NewGuid();
        await sut.AddAsync(NewReaction(e1, Guid.NewGuid()), TestContext.Current.CancellationToken);
        await sut.AddAsync(NewReaction(e2, Guid.NewGuid()), TestContext.Current.CancellationToken);
        await sut.AddAsync(NewReaction(e3, Guid.NewGuid()), TestContext.Current.CancellationToken);

        IReadOnlyList<Reaction> rows = await sut.GetByEntriesAsync([e1, e3], TestContext.Current.CancellationToken);
        rows.Select(r => r.EntryId).ShouldBe([e1, e3], ignoreOrder: true);
    }

    [Fact]
    public async Task GetByEntries_with_empty_input_returns_empty()
    {
        InMemoryReactionStore sut = new();
        IReadOnlyList<Reaction> rows = await sut.GetByEntriesAsync([], TestContext.Current.CancellationToken);
        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindAsync_returns_match_or_null()
    {
        InMemoryReactionStore sut = new();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await sut.AddAsync(NewReaction(entryId, userId, "👍"), TestContext.Current.CancellationToken);

        Reaction? hit = await sut.FindAsync(entryId, userId, "👍", TestContext.Current.CancellationToken);
        hit.ShouldNotBeNull();

        Reaction? miss = await sut.FindAsync(entryId, userId, "❤️", TestContext.Current.CancellationToken);
        miss.ShouldBeNull();
    }
}
