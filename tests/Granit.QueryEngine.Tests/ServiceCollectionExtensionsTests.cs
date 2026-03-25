using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitQueryEngine_registers_null_saved_view_store_reader()
    {
        ServiceCollection services = new();

        services.AddGranitQueryEngine();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ISavedViewStoreReader) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitQueryEngine_registers_null_saved_view_store_writer()
    {
        ServiceCollection services = new();

        services.AddGranitQueryEngine();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ISavedViewStoreWriter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitQueryEngine_does_not_replace_existing_reader()
    {
        ServiceCollection services = new();
        services.AddScoped<ISavedViewStoreReader, FakeSavedViewStoreReader>();

        services.AddGranitQueryEngine();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ISavedViewStoreReader store = scope.ServiceProvider.GetRequiredService<ISavedViewStoreReader>();
        store.ShouldBeOfType<FakeSavedViewStoreReader>();
    }

    [Fact]
    public void AddGranitQueryEngine_does_not_replace_existing_writer()
    {
        ServiceCollection services = new();
        services.AddScoped<ISavedViewStoreWriter, FakeSavedViewStoreWriter>();

        services.AddGranitQueryEngine();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ISavedViewStoreWriter store = scope.ServiceProvider.GetRequiredService<ISavedViewStoreWriter>();
        store.ShouldBeOfType<FakeSavedViewStoreWriter>();
    }

    [Fact]
    public void AddGranitQueryEngine_returns_service_collection_for_chaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitQueryEngine();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddQueryDefinition_registers_definition_as_singleton()
    {
        ServiceCollection services = new();

        services.AddQueryDefinition<TestEntity, TestQueryDef>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(QueryDefinition<TestEntity>) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddQueryDefinition_registers_descriptor_as_singleton()
    {
        ServiceCollection services = new();

        services.AddQueryDefinition<TestEntity, TestQueryDef>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryDefinitionDescriptor) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddQueryDefinition_descriptor_resolves_to_same_instance()
    {
        ServiceCollection services = new();
        services.AddQueryDefinition<TestEntity, TestQueryDef>();
        ServiceProvider provider = services.BuildServiceProvider();

        QueryDefinition<TestEntity> definition = provider.GetRequiredService<QueryDefinition<TestEntity>>();
        IQueryDefinitionDescriptor descriptor = provider.GetRequiredService<IQueryDefinitionDescriptor>();

        descriptor.ShouldBeSameAs(definition);
    }

    [Fact]
    public void AddQueryDefinition_returns_service_collection_for_chaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddQueryDefinition<TestEntity, TestQueryDef>();

        result.ShouldBeSameAs(services);
    }

    private sealed class TestQueryDef : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Entities";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder.Column(e => e.Name);
    }

    private sealed class FakeSavedViewStoreReader : ISavedViewStoreReader
    {
        public Task<IReadOnlyList<SavedView>> GetListAsync(
            string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SavedView>>([]);

        public Task<SavedView?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<SavedView?>(null);
    }

    private sealed class FakeSavedViewStoreWriter : ISavedViewStoreWriter
    {
        public Task CreateAsync(SavedView view, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(SavedView view, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetDefaultAsync(Guid id, string userId, string entityType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
