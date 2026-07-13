using Granit.Notifications.AwsSes.Extensions;
using Granit.Notifications.AwsSes.Options;
using Granit.Notifications.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

public sealed class SesEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsAwsSes_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSes();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAwsSes_RegistersAwsSesOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSes();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AwsSesOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAwsSes_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsAwsSes(opts =>
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
    public void AddGranitNotificationsAwsSes_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAwsSes();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsAwsSes_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsAwsSes(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsAwsSes_RegistersTransportFactory()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSes();

        services.ShouldContain(d =>
            d.ServiceType == typeof(Func<IAwsSesTransport>) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
