// =============================================================================
// Tests - EmailNotificationsServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitNotificationsEmail registers the expected services:
// INotificationChannel (EmailNotificationChannel), EmailChannelOptions binding,
// and correct handling of the optional configure delegate.
// =============================================================================

using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Extensions;
using Granit.Notifications.Email.Internal;
using Granit.Notifications.Email.Options;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsEmail_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.ImplementationType == typeof(EmailNotificationChannel) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotificationsEmail_RegistersEmailChannelOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<EmailChannelOptions>));
    }

    [Fact]
    public void AddGranitNotificationsEmail_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsEmail(opts =>
        {
            opts.Provider = "Brevo";
            opts.DefaultSenderEmail = "noreply@example.com";
            opts.DefaultSenderName = "Test App";
        });

        // Satisfies the keyed-provider startup validator — these tests assert options mechanics.
        services.AddKeyedSingleton("Brevo", (_, _) => Substitute.For<IEmailSender>());
        ServiceProvider sp = services.BuildServiceProvider();
        EmailChannelOptions options = sp.GetRequiredService<IOptions<EmailChannelOptions>>().Value;

        options.Provider.ShouldBe("Brevo");
        options.DefaultSenderEmail.ShouldBe("noreply@example.com");
        options.DefaultSenderName.ShouldBe("Test App");
    }

    [Fact]
    public void AddGranitNotificationsEmail_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsEmail();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsEmail_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsEmail(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsEmail_RegistersEmbeddedTemplateResolver()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsEmail();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateResolver) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
