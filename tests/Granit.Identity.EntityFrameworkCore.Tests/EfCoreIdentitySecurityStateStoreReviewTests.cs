using Granit.Guids;
using Granit.Identity.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

public sealed class EfCoreIdentitySecurityStateStoreReviewTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // SQLite (not InMemory): enforces the unique (UserId, SessionId) index that backs single-use semantics.
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TryRecordThenGet_RoundtripsDecision()
    {
        EfCoreIdentitySecurityStateStore store = CreateStore();

        bool first = await store.TryRecordSessionReviewDecisionAsync("u1", "s1", UserSessionReviewDecision.Denied, Now, Ct);

        first.ShouldBeTrue();
        (await store.GetSessionReviewDecisionAsync("u1", "s1", Ct)).ShouldBe(UserSessionReviewDecision.Denied);
    }

    [Fact]
    public async Task TryRecord_Repeat_ReturnsFalse_OneRow_FirstDecisionWins()
    {
        EfCoreIdentitySecurityStateStore store = CreateStore();

        await store.TryRecordSessionReviewDecisionAsync("u1", "s1", UserSessionReviewDecision.Confirmed, Now, Ct);
        bool second = await store.TryRecordSessionReviewDecisionAsync("u1", "s1", UserSessionReviewDecision.Denied, Now, Ct);

        second.ShouldBeFalse();
        await using IdentityDbContext db = _factory.CreateDbContext();
        (await db.UserSessionReviews.CountAsync(Ct)).ShouldBe(1);
        (await store.GetSessionReviewDecisionAsync("u1", "s1", Ct)).ShouldBe(UserSessionReviewDecision.Confirmed);
    }

    [Fact]
    public async Task TryRecord_ConcurrentFirstWrites_ExactlyOneWins()
    {
        // Two reviews of the same (user, session) racing both attempt an insert; SQLite trips the unique index
        // on one. Invariant: exactly one true, exactly one row — the single-use guarantee under concurrency.
        EfCoreIdentitySecurityStateStore store = CreateStore();

        bool[] results = await Task.WhenAll(
            store.TryRecordSessionReviewDecisionAsync("u1", "s1", UserSessionReviewDecision.Confirmed, Now, Ct),
            store.TryRecordSessionReviewDecisionAsync("u1", "s1", UserSessionReviewDecision.Denied, Now, Ct));

        results.Count(r => r).ShouldBe(1);
        await using IdentityDbContext db = _factory.CreateDbContext();
        (await db.UserSessionReviews.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task GetDecision_NotReviewed_ReturnsNull() =>
        (await CreateStore().GetSessionReviewDecisionAsync("u1", "missing", Ct)).ShouldBeNull();

    private EfCoreIdentitySecurityStateStore CreateStore()
    {
        IGuidGenerator guid = Substitute.For<IGuidGenerator>();
        guid.Create().Returns(_ => Guid.NewGuid());
        return new EfCoreIdentitySecurityStateStore(_factory, guid);
    }
}
