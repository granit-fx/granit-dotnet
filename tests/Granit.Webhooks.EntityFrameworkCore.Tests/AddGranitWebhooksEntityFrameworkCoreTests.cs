using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies the registration shape of
/// <see cref="WebhooksEntityFrameworkCoreHostApplicationBuilderExtensions
/// .AddGranitWebhooksEntityFrameworkCore"/> after the ADR-063 option refactor.
/// </summary>
public sealed class AddGranitWebhooksEntityFrameworkCoreTests
{
    [Fact]
    public void NullConfigure_Throws()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.Throw<ArgumentNullException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(configure: null!));
    }

    [Fact]
    public void Shared_WithoutConfigure_Throws()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(opts =>
            {
                opts.StorageMode = DualScopeStorageMode.Shared;
                // Configure intentionally left null
            }));

        ex.Message.ShouldContain("Configure");
        ex.Message.ShouldContain("Shared");
    }

    [Fact]
    public void Shared_DefaultMode_RegistersDbContextAndStores()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitWebhooksEntityFrameworkCore(opts =>
        {
            opts.Configure = b => b.UseInMemoryDatabase("webhooks-test");
        });

        ServiceDescriptor? subscriptionReader = builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IWebhookSubscriptionReader));
        subscriptionReader.ShouldNotBeNull();
        subscriptionReader.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        ServiceDescriptor? deliveryWriter = builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IWebhookDeliveryWriter));
        deliveryWriter.ShouldNotBeNull();
        deliveryWriter.ImplementationType.ShouldBe(typeof(EfWebhookDeliveryStore));

        ServiceDescriptor? dbContextFactory = builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<WebhooksDbContext>));
        dbContextFactory.ShouldNotBeNull();
    }

    [Fact]
    public void Segregated_WithSharedDatabaseStrategy_ThrowsBeforeRegistration()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = nameof(TenantIsolationStrategy.SharedDatabase),
        });

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(opts =>
            {
                opts.StorageMode = DualScopeStorageMode.Segregated;
                opts.ConfigureHost = b => b.UseInMemoryDatabase("host");
            }));

        // The DualScopeValidation guard fires before the NotSupportedException path.
        ex.Message.ShouldContain("Webhooks");
        ex.Message.ShouldContain("Segregated");
        ex.Message.ShouldContain("SharedDatabase");
        ex.Message.ShouldContain("ADR-063");
    }

    [Theory]
    [InlineData(nameof(TenantIsolationStrategy.SchemaPerTenant))]
    [InlineData(nameof(TenantIsolationStrategy.DatabasePerTenant))]
    public void Segregated_WithPhysicalIsolationStrategy_PassesValidationThenThrowsNotSupported(string strategyName)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = strategyName,
        });

        // Validation passes (no ADR-063 rejection), then registration throws NotSupported
        // because Phase 2B is not yet implemented.
        NotSupportedException ex = Should.Throw<NotSupportedException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(opts =>
            {
                opts.StorageMode = DualScopeStorageMode.Segregated;
                opts.ConfigureHost = b => b.UseInMemoryDatabase("host");
                opts.ConfigureSchemaPerTenant = (b, _) => b.UseInMemoryDatabase("tenant");
            }));

        ex.Message.ShouldContain("Segregated");
        ex.Message.ShouldContain("#2377");
    }
}
