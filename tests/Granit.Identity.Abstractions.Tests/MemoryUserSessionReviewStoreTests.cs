using Granit.Identity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class MemoryUserSessionReviewStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MemoryUserSessionReviewStore _sut = new();

    [Fact]
    public async Task TryRecord_FirstTime_RecordsAndReturnsTrue()
    {
        bool first = await _sut.TryRecordDecisionAsync("u1", "s1", UserSessionReviewDecision.Confirmed, Now, Ct);

        first.ShouldBeTrue();
        (await _sut.GetDecisionAsync("u1", "s1", Ct)).ShouldBe(UserSessionReviewDecision.Confirmed);
    }

    [Fact]
    public async Task TryRecord_Repeat_ReturnsFalse_AndKeepsFirstDecision()
    {
        await _sut.TryRecordDecisionAsync("u1", "s1", UserSessionReviewDecision.Denied, Now, Ct);

        bool second = await _sut.TryRecordDecisionAsync("u1", "s1", UserSessionReviewDecision.Confirmed, Now, Ct);

        second.ShouldBeFalse();
        // The first decision wins — a repeat never overwrites it.
        (await _sut.GetDecisionAsync("u1", "s1", Ct)).ShouldBe(UserSessionReviewDecision.Denied);
    }

    [Fact]
    public async Task GetDecision_NotReviewed_ReturnsNull() =>
        (await _sut.GetDecisionAsync("u1", "missing", Ct)).ShouldBeNull();

    [Fact]
    public async Task Reviews_AreIsolatedPerUserAndSession()
    {
        await _sut.TryRecordDecisionAsync("u1", "s1", UserSessionReviewDecision.Confirmed, Now, Ct);

        (await _sut.GetDecisionAsync("u2", "s1", Ct)).ShouldBeNull();
        (await _sut.GetDecisionAsync("u1", "s2", Ct)).ShouldBeNull();
    }
}
