using Granit.Notifications.AzureCommunicationServices.Email.Extensions;
using Granit.Notifications.AzureCommunicationServices.Email.Internal;
using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Granit.Notifications.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Email.Tests;

public sealed class AcsEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsAcsEmail_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsEmail();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_RegistersAcsEmailOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AcsEmailOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsAcsEmail(opts =>
        {
            opts.ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==";
            opts.DefaultSenderEmail = "test@example.com";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        AcsEmailOptions options = sp.GetRequiredService<IOptions<AcsEmailOptions>>().Value;

        options.ConnectionString.ShouldNotBeNull();
        options.DefaultSenderEmail.ShouldBe("test@example.com");
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAcsEmail();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsAcsEmail(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAcsEmailTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAcsEmail_RegistersEmailClient()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(Azure.Communication.Email.EmailClient) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
