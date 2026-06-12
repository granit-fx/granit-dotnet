using Granit.Bff.Options;
using Granit.Bff.UserSessions.Internal;
using Granit.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.UserSessions.Tests;

public sealed class BffUserSessionProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IBffTokenStore _store = Substitute.For<IBffTokenStore>();
    private readonly BffUserSessionProvider _sut;

    public BffUserSessionProviderTests()
    {
        GranitBffOptions options = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [new BffFrontendOptions { Name = "host" }],
        };
        _sut = new BffUserSessionProvider(_store, Microsoft.Extensions.Options.Options.Create(options));
    }

    private static BffTokenSet TokenSet(string userId, string? ua = "curl") =>
        new("at", null, null, Now.AddHours(1))
        {
            UserId = userId,
            UserAgent = ua,
            SessionCreatedAt = Now,
            LastAccessedAt = Now,
            IpAddress = "1.2.3.4",
        };

    [Fact]
    public async Task ListAsync_MapsStoreSessionsAndFlagsCurrent()
    {
        _store.GetSessionIdsByUserAsync("host", "user-1", Ct).Returns(["s1", "s2"]);
        _store.GetAsync("host", "s1", Ct).Returns(TokenSet("user-1"));
        _store.GetAsync("host", "s2", Ct).Returns(TokenSet("user-1"));

        IReadOnlyList<UserSessionDescriptor> result = await _sut.ListAsync("user-1", "s1", Ct);

        result.Count.ShouldBe(2);
        result[0].SessionId.ShouldBe("s1");
        result[0].IsCurrent.ShouldBeTrue();
        result[0].UserAgent.ShouldBe("curl");
        result[0].CreatedAt.ShouldBe(Now);
        result[1].IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_OnlyRevokesOwnSession()
    {
        _store.GetAsync("host", "s1", Ct).Returns(TokenSet("someone-else"));

        bool revoked = await _sut.RevokeAsync("user-1", "s1", Ct);

        revoked.ShouldBeFalse();
        await _store.DidNotReceive().RemoveAsync("host", "s1", Ct);
    }

    [Fact]
    public async Task RevokeAsync_RemovesMatchingSession()
    {
        _store.GetAsync("host", "s1", Ct).Returns(TokenSet("user-1"));

        bool revoked = await _sut.RevokeAsync("user-1", "s1", Ct);

        revoked.ShouldBeTrue();
        await _store.Received(1).RemoveAsync("host", "s1", Ct);
    }

    [Fact]
    public async Task RevokeOthersAsync_RemovesAllButCurrent()
    {
        _store.GetSessionIdsByUserAsync("host", "user-1", Ct).Returns(["s1", "s2", "s3"]);

        int revoked = await _sut.RevokeOthersAsync("user-1", "s2", Ct);

        revoked.ShouldBe(2);
        await _store.Received(1).RemoveAsync("host", "s1", Ct);
        await _store.Received(1).RemoveAsync("host", "s3", Ct);
        await _store.DidNotReceive().RemoveAsync("host", "s2", Ct);
    }
}
