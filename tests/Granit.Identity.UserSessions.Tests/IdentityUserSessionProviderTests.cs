using Granit.Identity.Models;
using Granit.Identity.UserSessions.Internal;
using Granit.UserSessions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.UserSessions.Tests;

public sealed class IdentityUserSessionProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IIdentitySessionManager _manager = Substitute.For<IIdentitySessionManager>();
    private readonly IdentityUserSessionProvider _sut;

    public IdentityUserSessionProviderTests() => _sut = new(_manager);

    private static IdentitySession Session(string id) =>
        new(id, "1.2.3.4", Now, Now, RememberMe: false, Clients: ["admin"]);

    [Fact]
    public async Task ListAsync_MapsAndFlagsCurrent()
    {
        _manager.GetUserSessionsAsync("user-1", Ct).Returns([Session("s1"), Session("s2")]);

        IReadOnlyList<UserSessionDescriptor> result = await _sut.ListAsync("user-1", "s2", Ct);

        result.Count.ShouldBe(2);
        result[0].SessionId.ShouldBe("s1");
        result[0].IsCurrent.ShouldBeFalse();
        result[0].IpAddress.ShouldBe("1.2.3.4");
        result[1].IsCurrent.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeAsync_UnknownSession_ReturnsFalseWithoutTerminating()
    {
        _manager.GetUserSessionsAsync("user-1", Ct).Returns([Session("s1")]);

        (await _sut.RevokeAsync("user-1", "missing", Ct)).ShouldBeFalse();
        await _manager.DidNotReceiveWithAnyArgs().TerminateSessionAsync(default!, default!, Ct);
    }

    [Fact]
    public async Task RevokeAsync_KnownSession_Terminates()
    {
        _manager.GetUserSessionsAsync("user-1", Ct).Returns([Session("s1")]);

        (await _sut.RevokeAsync("user-1", "s1", Ct)).ShouldBeTrue();
        await _manager.Received(1).TerminateSessionAsync("user-1", "s1", Ct);
    }

    [Fact]
    public async Task RevokeOthersAsync_TerminatesAllButCurrent()
    {
        _manager.GetUserSessionsAsync("user-1", Ct).Returns([Session("s1"), Session("s2"), Session("s3")]);

        int revoked = await _sut.RevokeOthersAsync("user-1", "s2", Ct);

        revoked.ShouldBe(2);
        await _manager.Received(1).TerminateSessionAsync("user-1", "s1", Ct);
        await _manager.Received(1).TerminateSessionAsync("user-1", "s3", Ct);
        await _manager.DidNotReceive().TerminateSessionAsync("user-1", "s2", Ct);
    }

    [Fact]
    public async Task DeviceProvider_MapsDeviceActivity()
    {
        IIdentitySessionManager manager = Substitute.For<IIdentitySessionManager>();
        manager.GetUserDeviceActivityAsync("user-1", Ct).Returns([
            new IdentityDeviceActivity("1.2.3.4", Now, "Desktop", "Windows", "10", "Chrome/120", Mobile: false, Current: true, Sessions: [Session("s1")])]);
        IdentityUserDeviceProvider sut = new(manager);

        IReadOnlyList<UserDevice> result = await sut.ListAsync("user-1", Ct);

        result.Count.ShouldBe(1);
        result[0].Kind.ShouldBe(DeviceKind.Browser);
        result[0].OperatingSystem.ShouldBe("Windows");
        result[0].Browser.ShouldBe("Chrome/120");
        result[0].SessionCount.ShouldBe(1);
    }
}
