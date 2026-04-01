using Granit.Notifications.Email.AwsSes.Extensions;
using Granit.Notifications.Email.AwsSes.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.AwsSes.Tests;

public sealed class SesEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsEmailAwsSes_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAwsSes();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsEmailAwsSes_RegistersAwsSesOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAwsSes();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AwsSesOptions>));
    }

    [Fact]
    public void AddGranitNotificationsEmailAwsSes_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsEmailAwsSes(opts =>
        {
            opts.Region = "eu-central-1";
            opts.DefaultSenderEmail = "test@example.com";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        AwsSesOptions options = sp.GetRequiredService<IOptions<AwsSesOptions>>().Value;

        options.Region.ShouldBe("eu-central-1");
        options.DefaultSenderEmail.ShouldBe("test@example.com");
    }

    [Fact]
    public void AddGranitNotificationsEmailAwsSes_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsEmailAwsSes();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsEmailAwsSes_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsEmailAwsSes(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsEmailAwsSes_RegistersTransportFactory()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmailAwsSes();

        services.ShouldContain(d =>
            d.ServiceType == typeof(Func<IAwsSesTransport>) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
