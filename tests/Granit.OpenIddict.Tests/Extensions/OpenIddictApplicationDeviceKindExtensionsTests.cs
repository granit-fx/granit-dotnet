using System.Collections.Immutable;
using System.Text.Json;
using Granit.OpenIddict.Extensions;
using Granit.UserSessions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Extensions;

/// <summary>
/// Tests for <see cref="OpenIddictApplicationDeviceKindExtensions"/> — the read/write helpers that persist a
/// declared <see cref="DeviceKind"/> on an OIDC application's <see cref="OpenIddictApplicationDescriptor.Properties"/> bag.
/// </summary>
public sealed class OpenIddictApplicationDeviceKindExtensionsTests
{
    [Theory]
    [InlineData(DeviceKind.Browser)]
    [InlineData(DeviceKind.MobileApp)]
    [InlineData(DeviceKind.Tv)]
    [InlineData(DeviceKind.ApiClient)]
    [InlineData(DeviceKind.Wearable)]
    public void SetDeviceKind_Then_GetDeviceKind_Roundtrip(DeviceKind kind)
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetDeviceKind(kind);

        descriptor.GetDeviceKind().ShouldBe(kind);
    }

    [Fact]
    public void SetDeviceKind_Writes_InvariantName_To_WellKnown_Key()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetDeviceKind(DeviceKind.MobileApp);

        descriptor.Properties.ShouldContainKey(
            OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey);
        JsonElement element = descriptor.Properties[
            OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey];
        element.ValueKind.ShouldBe(JsonValueKind.String);
        element.GetString().ShouldBe("MobileApp");
    }

    [Fact]
    public void SetDeviceKind_Null_Removes_Property()
    {
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.SetDeviceKind(DeviceKind.Tv);

        descriptor.SetDeviceKind(null);

        descriptor.Properties.ShouldNotContainKey(
            OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey);
        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void SetDeviceKind_Unknown_Removes_Property()
    {
        // Unknown is "not declared" — writing it must not persist a value the heuristic fallback would
        // then be skipped for. Symmetric with null.
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.SetDeviceKind(DeviceKind.DesktopApp);

        descriptor.SetDeviceKind(DeviceKind.Unknown);

        descriptor.Properties.ShouldNotContainKey(
            OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey);
        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void GetDeviceKind_ReturnsNull_WhenPropertyAbsent()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void GetDeviceKind_ReturnsNull_WhenValueIsNotAString()
    {
        // Forward-compatibility: a future schema could write a number under the same key. Treat unknown
        // shapes as "not declared" rather than throwing.
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey] =
            JsonSerializer.SerializeToElement(42);

        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void GetDeviceKind_ReturnsNull_WhenStringIsNotAnEnumMember()
    {
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey] =
            JsonSerializer.SerializeToElement("Hologram");

        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void GetDeviceKind_ReturnsNull_WhenValueIsUnknownName()
    {
        // "Unknown" persisted by an external writer means "not declared" — never SetDeviceKind writes it.
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey] =
            JsonSerializer.SerializeToElement("Unknown");

        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Fact]
    public void GetDeviceKind_IsCaseSensitive_OnEnumName()
    {
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey] =
            JsonSerializer.SerializeToElement("mobileapp");

        descriptor.GetDeviceKind().ShouldBeNull();
    }

    [Theory]
    [InlineData(DeviceKind.Browser)]
    [InlineData(DeviceKind.Tv)]
    [InlineData(DeviceKind.Embedded)]
    public async Task GetDeviceKindAsync_Reads_FromApplicationManager(DeviceKind kind)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new();

        ImmutableDictionary<string, JsonElement> properties =
            ImmutableDictionary<string, JsonElement>.Empty.Add(
                OpenIddictApplicationDeviceKindExtensions.DeviceKindPropertyKey,
                JsonSerializer.SerializeToElement(kind.ToString()));

        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(properties);

        DeviceKind? resolved = await manager.GetDeviceKindAsync(application, cancellationToken);

        resolved.ShouldBe(kind);
    }

    [Fact]
    public async Task GetDeviceKindAsync_ReturnsNull_WhenKeyAbsent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new();

        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(ImmutableDictionary<string, JsonElement>.Empty);

        DeviceKind? resolved = await manager.GetDeviceKindAsync(application, cancellationToken);

        resolved.ShouldBeNull();
    }
}
