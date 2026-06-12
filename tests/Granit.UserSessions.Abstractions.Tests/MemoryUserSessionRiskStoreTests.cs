using Granit.UserSessions.Internal;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Abstractions.Tests;

public sealed class MemoryUserSessionRiskStoreTests
{
    private static readonly DateTimeOffset AssessedAt = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MemoryUserSessionRiskStore _sut = new();

    [Fact]
    public async Task SetThenGet_ReturnsStoredVerdict()
    {
        UserSessionRiskVerdict verdict = new(UserSessionRiskLevel.High, ["impossible_travel"], AssessedAt);
        await _sut.SetAsync("user-1", "session-1", verdict, Ct);

        UserSessionRiskVerdict? result = await _sut.GetAsync("user-1", "session-1", Ct);

        result.ShouldBe(verdict);
    }

    [Fact]
    public async Task Get_UnknownSession_ReturnsNull() =>
        (await _sut.GetAsync("user-1", "missing", Ct)).ShouldBeNull();

    [Fact]
    public async Task Get_SameSessionIdDifferentUser_IsIsolated()
    {
        await _sut.SetAsync("user-1", "shared", new UserSessionRiskVerdict(UserSessionRiskLevel.High, [], AssessedAt), Ct);

        (await _sut.GetAsync("user-2", "shared", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetMany_ReturnsOnlyRecordedSessions()
    {
        await _sut.SetAsync("user-1", "a", new UserSessionRiskVerdict(UserSessionRiskLevel.Low, [], AssessedAt), Ct);
        await _sut.SetAsync("user-1", "c", new UserSessionRiskVerdict(UserSessionRiskLevel.Medium, [], AssessedAt), Ct);

        IReadOnlyDictionary<string, UserSessionRiskVerdict> result =
            await _sut.GetManyAsync("user-1", ["a", "b", "c"], Ct);

        result.Keys.ShouldBe(["a", "c"], ignoreOrder: true);
        result["a"].Level.ShouldBe(UserSessionRiskLevel.Low);
        result["c"].Level.ShouldBe(UserSessionRiskLevel.Medium);
    }
}
