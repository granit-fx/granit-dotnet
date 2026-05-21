using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class TenantIsolationFactoryRegistrationValidatorTests
{
    private sealed class CtxA(DbContextOptions<CtxA> options) : DbContext(options);
    private sealed class CtxB(DbContextOptions<CtxB> options) : DbContext(options);

    private static HostApplicationBuilder NewBuilder(string strategy)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MultiTenancy:TenantIsolation:Strategy"] = strategy,
        });
        return builder;
    }

    [Fact]
    public async Task SchemaPerTenant_configured_but_no_configureSchemaPerTenant_delegate_fails_at_ValidateOnStart()
    {
        HostApplicationBuilder builder = NewBuilder("SchemaPerTenant");

        builder.Services.AddGranitIsolatedDbContext<CtxA>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        using IHost host = builder.Build();

        OptionsValidationException ex = await Should.ThrowAsync<OptionsValidationException>(
            async () => await host.StartAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(nameof(CtxA));
        ex.Message.ShouldContain("SchemaPerTenant");
        ex.Message.ShouldContain("configureSchemaPerTenant");
    }

    [Fact]
    public async Task SchemaPerTenant_configured_with_delegate_passes()
    {
        HostApplicationBuilder builder = NewBuilder("SchemaPerTenant");

        builder.Services.AddGranitIsolatedDbContext<CtxA>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"),
            configureSchemaPerTenant: opts => opts.UseInMemoryDatabase("schema"));

        using IHost host = builder.Build();

        // Triggers all ValidateOnStart validators including the registration validator.
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        TenantIsolationOptions options = host.Services
            .GetRequiredService<IOptions<TenantIsolationOptions>>().Value;
        options.Strategy.ShouldBe(TenantIsolationStrategy.SchemaPerTenant);
    }

    [Fact]
    public async Task Multiple_isolated_contexts_missing_factory_emits_single_aggregated_error()
    {
        HostApplicationBuilder builder = NewBuilder("SchemaPerTenant");

        builder.Services.AddGranitIsolatedDbContext<CtxA>(
            configureShared: opts => opts.UseInMemoryDatabase("a-shared"));
        builder.Services.AddGranitIsolatedDbContext<CtxB>(
            configureShared: opts => opts.UseInMemoryDatabase("b-shared"));

        using IHost host = builder.Build();

        OptionsValidationException ex = await Should.ThrowAsync<OptionsValidationException>(
            async () => await host.StartAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(nameof(CtxA));
        ex.Message.ShouldContain(nameof(CtxB));
        // Aggregated: both context names appear in a single failure entry.
        ex.Failures.Count(f => f.Contains(nameof(CtxA)) && f.Contains(nameof(CtxB))).ShouldBe(1);
    }

    [Fact]
    public async Task SharedDatabase_always_registered_never_fails()
    {
        HostApplicationBuilder builder = NewBuilder("SharedDatabase");

        builder.Services.AddGranitIsolatedDbContext<CtxA>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        using IHost host = builder.Build();

        // Regression guard: validator must never fail when strategy is SharedDatabase.
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        TenantIsolationOptions options = host.Services
            .GetRequiredService<IOptions<TenantIsolationOptions>>().Value;
        options.Strategy.ShouldBe(TenantIsolationStrategy.SharedDatabase);
    }

    [Fact]
    public async Task DatabasePerTenant_without_delegate_fails_with_named_delegate_in_message()
    {
        HostApplicationBuilder builder = NewBuilder("DatabasePerTenant");

        builder.Services.AddGranitIsolatedDbContext<CtxA>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        using IHost host = builder.Build();

        OptionsValidationException ex = await Should.ThrowAsync<OptionsValidationException>(
            async () => await host.StartAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("configureDatabasePerTenant");
    }
}
