using System.Collections.Immutable;
using System.Text.Json;
using Granit.MultiTenancy;
using Granit.OpenIddict.Extensions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Extensions;

/// <summary>
/// Tests for <see cref="OpenIddictApplicationClientSideExtensions"/> — the read/write
/// helpers that persist a <see cref="MultiTenancySide"/> policy on an OIDC
/// application's <see cref="OpenIddictApplicationDescriptor.Properties"/> bag.
/// </summary>
public sealed class OpenIddictApplicationClientSideExtensionsTests
{
    [Theory]
    [InlineData(MultiTenancySide.Host)]
    [InlineData(MultiTenancySide.Tenant)]
    [InlineData(MultiTenancySide.Both)]
    public void SetClientSide_Then_GetClientSide_Roundtrip(MultiTenancySide side)
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetClientSide(side);

        descriptor.GetClientSide().ShouldBe(side);
    }

    [Fact]
    public void SetClientSide_Writes_String_To_WellKnown_Key()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.SetClientSide(MultiTenancySide.Tenant);

        descriptor.Properties.ShouldContainKey(
            OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey);
        JsonElement element = descriptor.Properties[
            OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey];
        element.ValueKind.ShouldBe(JsonValueKind.String);
        element.GetString().ShouldBe("Tenant");
    }

    [Fact]
    public void SetClientSide_Null_Removes_Property()
    {
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.SetClientSide(MultiTenancySide.Host);

        descriptor.SetClientSide(null);

        descriptor.Properties.ShouldNotContainKey(
            OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey);
        descriptor.GetClientSide().ShouldBeNull();
    }

    [Fact]
    public void GetClientSide_ReturnsNull_WhenPropertyAbsent()
    {
        OpenIddictApplicationDescriptor descriptor = new();

        descriptor.GetClientSide().ShouldBeNull();
    }

    [Fact]
    public void GetClientSide_ReturnsNull_WhenValueIsNotAString()
    {
        // Forward-compatibility: a future schema could write a number or object
        // under the same key. Rather than throw (which would brick sign-in on
        // every request), treat unknown shapes as "no policy".
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey] =
            JsonSerializer.SerializeToElement(42);

        descriptor.GetClientSide().ShouldBeNull();
    }

    [Fact]
    public void GetClientSide_ReturnsNull_WhenStringIsNotAnEnumMember()
    {
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey] =
            JsonSerializer.SerializeToElement("Martian");

        descriptor.GetClientSide().ShouldBeNull();
    }

    [Fact]
    public void GetClientSide_IsCaseSensitive_OnEnumName()
    {
        // Persisted values are produced by our own SetClientSide which uses the
        // invariant enum name ("Host" / "Tenant" / "Both"). A lowercase "host"
        // would indicate either a hand-edited record or another writer — don't
        // interpret it, treat as no policy. Matches the forward-compat stance
        // of GetClientSide_ReturnsNull_WhenStringIsNotAnEnumMember.
        OpenIddictApplicationDescriptor descriptor = new();
        descriptor.Properties[OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey] =
            JsonSerializer.SerializeToElement("host");

        descriptor.GetClientSide().ShouldBeNull();
    }

    [Theory]
    [InlineData(MultiTenancySide.Host)]
    [InlineData(MultiTenancySide.Tenant)]
    [InlineData(MultiTenancySide.Both)]
    public async Task GetClientSideAsync_Reads_FromApplicationManager(MultiTenancySide side)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new();

        ImmutableDictionary<string, JsonElement> properties =
            ImmutableDictionary<string, JsonElement>.Empty.Add(
                OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey,
                JsonSerializer.SerializeToElement(side.ToString()));

        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(properties);

        MultiTenancySide? resolved = await manager.GetClientSideAsync(application, cancellationToken);

        resolved.ShouldBe(side);
    }

    [Fact]
    public async Task GetClientSideAsync_ReturnsNull_WhenKeyAbsent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new();

        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(ImmutableDictionary<string, JsonElement>.Empty);

        MultiTenancySide? resolved = await manager.GetClientSideAsync(application, cancellationToken);

        resolved.ShouldBeNull();
    }
}
