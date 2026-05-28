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
            .FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<WebhooksHostDbContext>));
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
    public void Segregated_WithPhysicalIsolationStrategy_RegistersBothFactories(string strategyName)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = strategyName,
        });

        builder.AddGranitWebhooksEntityFrameworkCore(opts =>
        {
            opts.StorageMode = DualScopeStorageMode.Segregated;
            opts.ConfigureHost = b => b.UseInMemoryDatabase("host");
            opts.ConfigureSchemaPerTenant = b => b.UseInMemoryDatabase("tenant-schema");
            opts.ConfigureDatabasePerTenant = (b, _) => b.UseInMemoryDatabase("tenant-db");
        });

        ServiceDescriptor? hostFactory = builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<WebhooksHostDbContext>));
        hostFactory.ShouldNotBeNull();

        // Tenant factory is keyed by isolation strategy under AddGranitIsolatedDbContext.
        bool hasTenantMarker = builder.Services
            .Any(d => d.ServiceType.FullName?.Contains("WebhooksTenantDbContext") == true);
        hasTenantMarker.ShouldBeTrue();
    }

    [Fact]
    public void Segregated_WithoutConfigureHost_Throws()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = nameof(TenantIsolationStrategy.SchemaPerTenant),
        });

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(opts =>
            {
                opts.StorageMode = DualScopeStorageMode.Segregated;
                opts.ConfigureSchemaPerTenant = b => b.UseInMemoryDatabase("tenant");
                // ConfigureHost intentionally null
            }));

        ex.Message.ShouldContain("ConfigureHost");
    }

    [Fact]
    public void Segregated_WithoutAnyTenantCallback_Throws()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = nameof(TenantIsolationStrategy.SchemaPerTenant),
        });

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddGranitWebhooksEntityFrameworkCore(opts =>
            {
                opts.StorageMode = DualScopeStorageMode.Segregated;
                opts.ConfigureHost = b => b.UseInMemoryDatabase("host");
                // Neither tenant callback set
            }));

        ex.Message.ShouldContain("ConfigureSchemaPerTenant");
        ex.Message.ShouldContain("ConfigureDatabasePerTenant");
    }
}
