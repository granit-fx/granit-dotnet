using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Internal;
using Granit.OpenIddict.Server.SenderConstraining;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Server.Tests.Internal;

public sealed class SenderConstrainingOptionsValidatorTests
{
    private sealed class FakeMechanism(SenderConstrainingMode mode) : ISenderConstrainingMechanism
    {
        public SenderConstrainingMode Mode { get; } = mode;
    }

    private static ValidateOptionsResult Validate(
        GranitOpenIddictOptions options, params ISenderConstrainingMechanism[] mechanisms) =>
        new SenderConstrainingOptionsValidator(mechanisms).Validate(name: null, options);

    [Fact]
    public void Fapi2WithNoSenderConstraining_Fails()
    {
        ValidateOptionsResult result = Validate(
            new GranitOpenIddictOptions { EnableFapi2Profile = true, SenderConstraining = SenderConstrainingMode.None });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void DPoPModeWithoutMechanism_Fails()
    {
        ValidateOptionsResult result = Validate(
            new GranitOpenIddictOptions { SenderConstraining = SenderConstrainingMode.DPoP });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void DPoPModeWithDPoPMechanism_Succeeds()
    {
        ValidateOptionsResult result = Validate(
            new GranitOpenIddictOptions { EnableFapi2Profile = true, SenderConstraining = SenderConstrainingMode.DPoP },
            new FakeMechanism(SenderConstrainingMode.DPoP));

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void NoneWithoutFapi2_Succeeds()
    {
        ValidateOptionsResult result = Validate(
            new GranitOpenIddictOptions { SenderConstraining = SenderConstrainingMode.None });

        result.Succeeded.ShouldBeTrue();
    }
}
