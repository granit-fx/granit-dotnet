using Granit.Notifications.Email.Extensions;
using Granit.Notifications.Email.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailNotificationsServiceCollectionExtensionsAdditionalTests
{
    [Fact]
    public void AddGranitNotificationsEmail_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsEmail();

        // Satisfies the keyed-provider startup validator — these tests assert options mechanics.
        services.AddKeyedSingleton("Smtp", (_, _) => Substitute.For<IEmailSender>());
        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<EmailChannelOptions> options = sp.GetRequiredService<IOptions<EmailChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsEmail_WithConfigure_AppliesConfiguration()
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
        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<EmailChannelOptions> options = sp.GetRequiredService<IOptions<EmailChannelOptions>>();

        options.Value.Provider.ShouldBe("Brevo");
        options.Value.DefaultSenderEmail.ShouldBe("noreply@example.com");
        options.Value.DefaultSenderName.ShouldBe("Test App");
    }

    [Fact]
    public void AddGranitNotificationsEmail_DefaultOptions_HasSmtpProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsEmail();

        // Satisfies the keyed-provider startup validator — these tests assert options mechanics.
        services.AddKeyedSingleton("Smtp", (_, _) => Substitute.For<IEmailSender>());
        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<EmailChannelOptions> options = sp.GetRequiredService<IOptions<EmailChannelOptions>>();

        options.Value.Provider.ShouldBe("Smtp");
    }
}
