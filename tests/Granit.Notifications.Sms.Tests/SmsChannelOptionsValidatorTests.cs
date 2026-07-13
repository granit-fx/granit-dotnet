using Granit.Notifications.Sms.Extensions;
using Granit.Notifications.Sms.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class SmsChannelOptionsValidatorTests
{
    private static SmsChannelOptionsValidator BuildValidator(bool keyIsRegistered)
    {
        IServiceProviderIsKeyedService keyed = Substitute.For<IServiceProviderIsKeyedService>();
        keyed.IsKeyedService(typeof(ISmsSender), Arg.Any<object?>()).Returns(keyIsRegistered);
        return new SmsChannelOptionsValidator(keyed);
    }

    [Fact]
    public void EmptyProvider_Fails()
    {
        ValidateOptionsResult result = BuildValidator(keyIsRegistered: true)
            .Validate(null, new SmsChannelOptions { Provider = "" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Provider is required");
    }

    [Fact]
    public void UnknownProviderKey_FailsWithExplicitMessage()
    {
        ValidateOptionsResult result = BuildValidator(keyIsRegistered: false)
            .Validate(null, new SmsChannelOptions { Provider = "DoesNotExist" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("DoesNotExist");
        result.FailureMessage.ShouldContain("provider package");
    }

    [Fact]
    public void RegisteredProviderKey_Succeeds() =>
        BuildValidator(keyIsRegistered: true)
            .Validate(null, new SmsChannelOptions { Provider = "SomeProvider" })
            .Succeeded.ShouldBeTrue();

    /// <summary>
    /// End-to-end: a misconfigured provider key fails at options resolution (ValidateOnStart
    /// surfaces this at boot) instead of GetRequiredKeyedService throwing at the first send.
    /// </summary>
    [Fact]
    public void MisconfiguredHost_FailsOptionsValidation_NotFirstSend()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Sms:Provider"] = "NotARealProvider",
        }).Build());
        services.AddGranitNotificationsSms();

        using ServiceProvider sp = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<SmsChannelOptions>>().Value);
    }
}
