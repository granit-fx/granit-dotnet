using Granit.Features.EntityFrameworkCore.Entities;
using Granit.Features.EntityFrameworkCore.Extensions;
using Granit.Features.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class FeaturesEfCoreDiRegistrationTests
{
    // Stub IFeatureStoreReader/Writer to simulate a prior registration (e.g. InMemoryFeatureStore which is internal).
    private sealed class StubFeatureStore : IFeatureStoreReader, IFeatureStoreWriter
    {
        public Task<string?> GetOrNullAsync(string featureName, string? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string featureName, string? tenantId, string value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string featureName, string? tenantId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // AddGranitFeaturesEntityFrameworkCore
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitFeaturesEntityFrameworkCore_RegistersEfCoreFeatureStore()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddScoped<IFeatureStoreReader, StubFeatureStore>(); // simulate AddGranitFeatures()
        builder.Services.AddScoped<IFeatureStoreWriter, StubFeatureStore>();

        builder.AddGranitFeaturesEntityFrameworkCore(opts =>
            opts.UseInMemoryDatabase("features-test"));

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        IFeatureStoreReader reader = sp.CreateScope().ServiceProvider.GetRequiredService<IFeatureStoreReader>();
        IFeatureStoreWriter writer = sp.CreateScope().ServiceProvider.GetRequiredService<IFeatureStoreWriter>();

        reader.ShouldBeOfType<EfCoreFeatureStore>("AddGranitFeaturesEntityFrameworkCore must replace the pre-registered reader with EfCoreFeatureStore");
        writer.ShouldBeOfType<EfCoreFeatureStore>("AddGranitFeaturesEntityFrameworkCore must replace the pre-registered writer with EfCoreFeatureStore");
    }

    [Fact]
    public void AddGranitFeaturesEntityFrameworkCore_ReturnsBuilder_ForChaining()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        IHostApplicationBuilder result = builder.AddGranitFeaturesEntityFrameworkCore(opts =>
            opts.UseInMemoryDatabase("features-chain"));

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitFeaturesEntityFrameworkCore_RegistersDbContextFactory_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitFeaturesEntityFrameworkCore(opts =>
            opts.UseInMemoryDatabase("features-factory"));

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<FeaturesDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
