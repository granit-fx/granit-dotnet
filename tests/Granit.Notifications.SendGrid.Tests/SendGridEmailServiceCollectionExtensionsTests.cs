using Granit.Notifications.Email;
using Granit.Notifications.SendGrid.Extensions;
using Granit.Notifications.SendGrid.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SendGrid.Tests;

public sealed class SendGridEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsSendGrid_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSendGrid();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSendGrid_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSendGrid(opts =>
        {
            opts.ApiKey = "SG.test-key";
            opts.DefaultSenderEmail = "test@example.com";
            opts.DefaultSenderName = "Test";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        SendGridEmailOptions options = sp.GetRequiredService<IOptions<SendGridEmailOptions>>().Value;

        options.ApiKey.ShouldBe("SG.test-key");
        options.DefaultSenderEmail.ShouldBe("test@example.com");
        options.DefaultSenderName.ShouldBe("Test");
    }

    [Fact]
    public void AddGranitNotificationsSendGrid_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSendGrid();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsSendGrid_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSendGrid(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsSendGrid_RegistersHttpClient()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSendGrid();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IHttpClientFactory));
    }
}
