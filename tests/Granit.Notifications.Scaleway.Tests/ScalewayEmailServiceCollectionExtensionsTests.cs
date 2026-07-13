using Granit.Notifications.Email;
using Granit.Notifications.Scaleway.Extensions;
using Granit.Notifications.Scaleway.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Scaleway.Tests;

public sealed class ScalewayEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsScaleway_RegistersKeyedEmailSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsScaleway();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsScaleway_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsScaleway(opts =>
        {
            opts.SecretKey = "scw-test-key";
            opts.ProjectId = "project-abc";
            opts.DefaultSenderEmail = "test@example.com";
            opts.DefaultSenderName = "Test";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        ScalewayEmailOptions options = sp.GetRequiredService<IOptions<ScalewayEmailOptions>>().Value;

        options.SecretKey.ShouldBe("scw-test-key");
        options.ProjectId.ShouldBe("project-abc");
        options.DefaultSenderEmail.ShouldBe("test@example.com");
        options.DefaultSenderName.ShouldBe("Test");
    }

    [Fact]
    public void AddGranitNotificationsScaleway_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsScaleway();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsScaleway_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsScaleway(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsScaleway_RegistersHttpClient()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsScaleway();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void AddGranitNotificationsScaleway_SetsXAuthTokenHeader()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Scaleway:SecretKey"] = "my-secret",
                ["Notifications:Scaleway:ProjectId"] = "proj-1",
                ["Notifications:Scaleway:DefaultSenderEmail"] = "a@b.com",
            })
            .Build());
        services.AddGranitNotificationsScaleway();

        ServiceProvider sp = services.BuildServiceProvider();
        IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient("Scaleway");

        client.DefaultRequestHeaders.TryGetValues("X-Auth-Token", out IEnumerable<string>? values).ShouldBeTrue();
        values!.ShouldContain("my-secret");
    }
}
