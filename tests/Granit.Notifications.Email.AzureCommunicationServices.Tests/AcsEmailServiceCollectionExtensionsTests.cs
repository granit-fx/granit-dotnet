using Granit.Notifications.Email.AzureCommunicationServices.Extensions;
using Granit.Notifications.Email.AzureCommunicationServices.Internal;
using Granit.Notifications.Email.AzureCommunicationServices.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.AzureCommunicationServices.Tests;

public sealed class AcsEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsEmailAcs_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAcs();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsEmailAcs_RegistersAcsEmailOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAcs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AcsEmailOptions>));
    }

    [Fact]
    public void AddGranitNotificationsEmailAcs_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsEmailAcs(opts =>
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
    public void AddGranitNotificationsEmailAcs_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsEmailAcs();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsEmailAcs_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsEmailAcs(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsEmailAcs_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAcs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAcsEmailTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsEmailAcs_RegistersEmailClient()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAcs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(Azure.Communication.Email.EmailClient) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
