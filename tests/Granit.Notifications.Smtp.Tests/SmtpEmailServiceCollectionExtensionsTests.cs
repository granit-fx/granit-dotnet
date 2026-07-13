using Granit.Notifications.Email;
using Granit.Notifications.Smtp.Extensions;
using Granit.Notifications.Smtp.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Smtp.Tests;

public sealed class SmtpEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsSmtp_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSmtp();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSmtp_RegistersSmtpOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSmtp();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<SmtpOptions>));
    }

    [Fact]
    public void AddGranitNotificationsSmtp_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSmtp(opts =>
        {
            opts.Host = "mail.example.com";
            opts.Port = 465;
        });

        ServiceProvider sp = services.BuildServiceProvider();
        SmtpOptions options = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;

        options.Host.ShouldBe("mail.example.com");
        options.Port.ShouldBe(465);
    }

    [Fact]
    public void AddGranitNotificationsSmtp_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSmtp();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsSmtp_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSmtp(configure: null));
    }
}
