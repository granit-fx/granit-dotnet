using Granit.Identity.Local.Options;
using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class OptionsTests
{
    [Fact]
    public void GranitOpenIddictOptions_HasSensibleDefaults()
    {
        GranitOpenIddictOptions options = new();

        options.Issuer.ShouldBeNull();
        options.EnableEntityCaching.ShouldBeFalse();
        options.UseReferenceTokens.ShouldBeFalse();
    }

    [Fact]
    public void GranitOpenIddictClientOptions_HasSensibleDefaults()
    {
        GranitOpenIddictClientOptions options = new();

        options.AutoRegisterExternalUsers.ShouldBeTrue();
        options.Providers.ShouldBeEmpty();
        GranitOpenIddictClientOptions.SectionName.ShouldBe("OpenIddict:Client");
    }

    [Fact]
    public void GranitOpenIddictSeedingOptions_HasSensibleDefaults()
    {
        GranitOpenIddictSeedingOptions options = new();

        options.Applications.ShouldBeEmpty();
        options.Scopes.ShouldBeEmpty();
        GranitOpenIddictSeedingOptions.SectionName.ShouldBe("OpenIddict:Seeding");
    }

    [Fact]
    public void GranitPasskeyOptions_HasSensibleDefaults()
    {
        GranitPasskeyOptions options = new();

        options.ServerDomain.ShouldBeEmpty();
        options.AuthenticatorTimeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.ChallengeSize.ShouldBe(32);
        GranitPasskeyOptions.SectionName.ShouldBe("Identity:Passkeys");
    }

    [Fact]
    public void ExternalProviderOptions_HasSensibleDefaults()
    {
        ExternalProviderOptions options = new();

        options.Name.ShouldBeEmpty();
        options.ClientId.ShouldBeEmpty();
        options.ClientSecret.ShouldBeEmpty();
        options.Scopes.ShouldBe(new[] { "openid", "profile", "email" });
    }
}
