using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class UserDeviceGroupingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static UserSessionDescriptor Session(string? ip, DateTimeOffset lastSeen, DeviceKind kind = DeviceKind.Unknown) =>
        new("s", "u", IsCurrent: false, T0, lastSeen, "ua", ip, Location: null, kind);

    [Fact]
    public void ByIpAddress_GroupsByIp_WithSessionCountAndLatestLastSeen()
    {
        IReadOnlyList<UserDevice> devices = UserDeviceGrouping.ByIpAddress(
        [
            Session("1.1.1.1", T0),
            Session("1.1.1.1", T0.AddHours(2)),
            Session("2.2.2.2", T0.AddHours(1)),
        ]);

        devices.Count.ShouldBe(2);
        UserDevice ip1 = devices.Single(d => d.DeviceId == "1.1.1.1");
        ip1.SessionCount.ShouldBe(2);
        ip1.LastSeen.ShouldBe(T0.AddHours(2));
        devices.Single(d => d.DeviceId == "2.2.2.2").SessionCount.ShouldBe(1);
    }

    [Fact]
    public void ByIpAddress_MostSpecificKindWins_ElseBrowser()
    {
        // A group with a declared MobileApp session and a plain one surfaces MobileApp.
        UserDeviceGrouping.ByIpAddress(
            [Session("1.1.1.1", T0, DeviceKind.Browser), Session("1.1.1.1", T0, DeviceKind.MobileApp)])
            .Single().Kind.ShouldBe(DeviceKind.MobileApp);

        // A group with only Unknown/Browser defaults to Browser (never surfaces Unknown).
        UserDeviceGrouping.ByIpAddress([Session("2.2.2.2", T0, DeviceKind.Unknown)])
            .Single().Kind.ShouldBe(DeviceKind.Browser);
    }

    [Fact]
    public void ByIpAddress_NoSessions_ReturnsEmpty() =>
        UserDeviceGrouping.ByIpAddress([]).ShouldBeEmpty();

    [Fact]
    public void ByIpAddress_NullIp_BucketsUnderUnknownSignature() =>
        UserDeviceGrouping.ByIpAddress([Session(null, T0)]).Single().DeviceId.ShouldBe("unknown");
}
