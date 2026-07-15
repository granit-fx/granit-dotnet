using Granit.Identity.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class DefaultUserSessionManagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IUserSessionProvider _sessions = Substitute.For<IUserSessionProvider>();
    private readonly IUserDeviceProvider _devices = Substitute.For<IUserDeviceProvider>();
    private readonly IIdentitySecurityStateStore _securityState = Substitute.For<IIdentitySecurityStateStore>();
    private readonly DefaultUserSessionManager _sut;

    public DefaultUserSessionManagerTests() =>
        _sut = new(_sessions, _devices, _securityState, TimeProvider.System);

    private static UserSessionDescriptor Session(string id) =>
        new(id, "user-1", IsCurrent: id == "s1", CreatedAt: DateTimeOffset.UnixEpoch,
            LastAccessedAt: null, UserAgent: null, IpAddress: null, Location: null);

    [Fact]
    public async Task ListAsync_AttachesPersistedRiskVerdictPerSession()
    {
        _sessions.ListAsync("user-1", "s1", Ct).Returns([Session("s1"), Session("s2")]);
        UserSessionRiskVerdict verdict = new(UserSessionRiskLevel.High, ["impossible_travel"], DateTimeOffset.UnixEpoch);
        _securityState.GetSessionRisksAsync("user-1", Arg.Any<IReadOnlyCollection<string>>(), Ct)
            .Returns(new Dictionary<string, UserSessionRiskVerdict> { ["s2"] = verdict });

        IReadOnlyList<UserSessionView> result = await _sut.ListAsync("user-1", "s1", Ct);

        result.Count.ShouldBe(2);
        result[0].Session.SessionId.ShouldBe("s1");
        result[0].Risk.ShouldBeNull();
        result[1].Risk.ShouldBe(verdict);
    }

    [Fact]
    public async Task ListAsync_NoSessions_SkipsRiskLookup()
    {
        _sessions.ListAsync("user-1", null, Ct).Returns([]);

        (await _sut.ListAsync("user-1", null, Ct)).ShouldBeEmpty();
        await _securityState.DidNotReceiveWithAnyArgs().GetSessionRisksAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task RevokeAsync_DispatchesToProvider()
    {
        _sessions.RevokeAsync("user-1", "s2", Ct).Returns(true);

        (await _sut.RevokeAsync("user-1", "s2", Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeOthersAsync_DispatchesToProvider()
    {
        _sessions.RevokeOthersAsync("user-1", "s1", Ct).Returns(3);

        (await _sut.RevokeOthersAsync("user-1", "s1", Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task RevokeAllAsync_RevokesEverySessionIncludingCurrent()
    {
        _sessions.ListAsync("user-1", null, Ct).Returns([Session("s1"), Session("s2")]);
        _sessions.RevokeAsync("user-1", "s1", Ct).Returns(true);
        _sessions.RevokeAsync("user-1", "s2", Ct).Returns(true);

        int revoked = await _sut.RevokeAllAsync("user-1", Ct);

        revoked.ShouldBe(2);
        await _sessions.Received(1).RevokeAsync("user-1", "s1", Ct);
        await _sessions.Received(1).RevokeAsync("user-1", "s2", Ct);
    }

    [Fact]
    public async Task RevokeAllAsync_CountsOnlyRevokedSessions()
    {
        _sessions.ListAsync("user-1", null, Ct).Returns([Session("s1"), Session("s2")]);
        _sessions.RevokeAsync("user-1", "s1", Ct).Returns(true);
        _sessions.RevokeAsync("user-1", "s2", Ct).Returns(false);

        (await _sut.RevokeAllAsync("user-1", Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task RevokeAllAsync_BlankUserId_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.RevokeAllAsync("", Ct));

    [Fact]
    public async Task ListDevicesAsync_DelegatesToProvider()
    {
        UserDevice device = new("d1", DeviceKind.Browser, "Windows", "Chrome", null, 2, null);
        _devices.ListAsync("user-1", Ct).Returns([device]);

        (await _sut.ListDevicesAsync("user-1", Ct)).ShouldBe([device]);
    }

    [Fact]
    public async Task RevokeAsync_BlankUserId_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.RevokeAsync("", "s1", Ct));
}
