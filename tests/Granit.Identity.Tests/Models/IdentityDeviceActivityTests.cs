using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Models;

public sealed class IdentityDeviceActivityTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        DateTimeOffset lastAccess = DateTimeOffset.UtcNow;
        List<IdentitySession> sessions =
        [
            new("sess-1", "1.2.3.4", lastAccess, lastAccess, false, ["app-1"]),
        ];

        var device = new IdentityDeviceActivity(
            IpAddress: "10.0.0.1",
            LastAccess: lastAccess,
            Device: "Desktop",
            OperatingSystem: "Windows",
            OperatingSystemVersion: "11",
            Browser: "Chrome/120.0",
            Mobile: false,
            Current: true,
            Sessions: sessions);

        device.IpAddress.ShouldBe("10.0.0.1");
        device.LastAccess.ShouldBe(lastAccess);
        device.Device.ShouldBe("Desktop");
        device.OperatingSystem.ShouldBe("Windows");
        device.OperatingSystemVersion.ShouldBe("11");
        device.Browser.ShouldBe("Chrome/120.0");
        device.Mobile.ShouldBeFalse();
        device.Current.ShouldBeTrue();
        device.Sessions.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_AllowsNullDeviceFields()
    {
        DateTimeOffset lastAccess = DateTimeOffset.UtcNow;

        var device = new IdentityDeviceActivity(
            IpAddress: null,
            LastAccess: lastAccess,
            Device: null,
            OperatingSystem: null,
            OperatingSystemVersion: null,
            Browser: null,
            Mobile: false,
            Current: false,
            Sessions: []);

        device.IpAddress.ShouldBeNull();
        device.Device.ShouldBeNull();
        device.OperatingSystem.ShouldBeNull();
        device.OperatingSystemVersion.ShouldBeNull();
        device.Browser.ShouldBeNull();
        device.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        DateTimeOffset lastAccess = DateTimeOffset.UtcNow;
        var original = new IdentityDeviceActivity(
            "10.0.0.1", lastAccess, "Desktop", "Windows", "11", "Chrome", false, true, []);

        IdentityDeviceActivity modified = original with { Mobile = true, Current = false };

        modified.Mobile.ShouldBeTrue();
        modified.Current.ShouldBeFalse();
        original.Mobile.ShouldBeFalse();
        original.Current.ShouldBeTrue();
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        DateTimeOffset lastAccess = DateTimeOffset.UtcNow;
        var device = new IdentityDeviceActivity(
            "10.0.0.1", lastAccess, "Desktop", "Windows", "11", "Chrome", false, true, []);

        string str = device.ToString();

        str.ShouldContain("IdentityDeviceActivity");
    }
}
