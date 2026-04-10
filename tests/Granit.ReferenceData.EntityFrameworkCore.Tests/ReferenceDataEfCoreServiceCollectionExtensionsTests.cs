using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class ReferenceDataEfCoreServiceCollectionExtensionsTests
{
    private sealed class TestEntityConfiguration
        : Granit.ReferenceData.EntityFrameworkCore.Internal.ReferenceDataEntityTypeConfiguration<TestEntity>
    {
        public TestEntityConfiguration() : base("ref_test_entities") { }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureReferenceData(new TestEntityConfiguration());
        }
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        services.AddSingleton<IOptions<ReferenceDataOptions>>(
            Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions()));
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        services.AddReferenceDataStore<TestEntity, TestDbContext>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddReferenceDataStore_RegistersStoreReader()
    {
        using ServiceProvider sp = BuildProvider();

        IReferenceDataStoreReader<TestEntity> reader =
            sp.GetRequiredService<IReferenceDataStoreReader<TestEntity>>();

        reader.ShouldNotBeNull();
    }

    [Fact]
    public void AddReferenceDataStore_RegistersStoreWriter()
    {
        using ServiceProvider sp = BuildProvider();

        IReferenceDataStoreWriter<TestEntity> writer =
            sp.GetRequiredService<IReferenceDataStoreWriter<TestEntity>>();

        writer.ShouldNotBeNull();
    }

#pragma warning disable CS0618 // IDataSeedContributor: testing backward compat registration
    [Fact]
    public void AddReferenceDataStore_RegistersDataSeedContributor()
    {
        using ServiceProvider sp = BuildProvider();

        IEnumerable<IDataSeedContributor> contributors =
            sp.GetServices<IDataSeedContributor>();

        contributors.ShouldNotBeEmpty();
    }
#pragma warning restore CS0618

    [Fact]
    public void AddReferenceDataStore_ReaderAndWriter_AreSameInstance()
    {
        using ServiceProvider sp = BuildProvider();

        using IServiceScope scope = sp.CreateScope();
        IReferenceDataStoreReader<TestEntity> reader =
            scope.ServiceProvider.GetRequiredService<IReferenceDataStoreReader<TestEntity>>();
        IReferenceDataStoreWriter<TestEntity> writer =
            scope.ServiceProvider.GetRequiredService<IReferenceDataStoreWriter<TestEntity>>();

        reader.ShouldBeSameAs(writer);
    }

    [Fact]
    public void AddReferenceDataStore_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        services.AddSingleton<IOptions<ReferenceDataOptions>>(
            Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions()));

        IServiceCollection result = services.AddReferenceDataStore<TestEntity, TestDbContext>();

        result.ShouldBeSameAs(services);
    }
}
