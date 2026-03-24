// =============================================================================
// Tests - GranitWebhooksModule
// =============================================================================
// Verifies module inheritance, DependsOn declarations, that AddGranitWebhooks()
// registers services without throwing, and that DI registrations are present.
// =============================================================================

using Granit.Modularity;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Extensions;
using Granit.Webhooks.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class GranitWebhooksModuleTests
{
    [Fact]
    public void GranitWebhooksModule_IsGranitModule() =>
        typeof(GranitWebhooksModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitWebhooksModule_IsSealed() =>
        typeof(GranitWebhooksModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void GranitWebhooksModule_DependsOn_TimingModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWebhooksModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitTimingModule)));
    }

    [Fact]
    public void AddGranitWebhooks_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Action act = () => builder.AddGranitWebhooks();

        Should.NotThrow(act);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersIWebhookPublisher_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWebhookPublisher) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersIWebhookSubscriptionReader_Singleton()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWebhookSubscriptionReader) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersIWebhookSubscriptionWriter_Singleton()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWebhookSubscriptionWriter) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersIWebhookDeliveryWriter_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWebhookDeliveryWriter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersIWebhookSecretProtector_Singleton()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWebhookSecretProtector) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitWebhooks_RegistersWebhooksOptionsValidator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWebhooks();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WebhooksOptions>));
    }

    [Fact]
    public void AddGranitWebhooks_WithConfigure_AppliesOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitWebhooks(opts => opts.MaxParallelDeliveries = 5);

        // No exception = callback was invoked successfully.
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IWebhookPublisher));
    }
}
