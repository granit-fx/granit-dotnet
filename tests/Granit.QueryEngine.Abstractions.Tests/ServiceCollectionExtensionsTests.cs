using Granit.QueryEngine.Diagnostics;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitQueryEngine_registers_options_singleton()
    {
        ServiceCollection services = new();
        services.AddOptions<QueryEngineOptions>();

        services.AddGranitQueryEngine();

        services.ShouldContain(d =>
            d.ServiceType == typeof(QueryEngineOptions) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitQueryEngine_registers_metrics_singleton()
    {
        ServiceCollection services = new();

        services.AddGranitQueryEngine();

        services.ShouldContain(d =>
            d.ServiceType == typeof(QueryEngineMetrics) &&
            d.Lifetime == ServiceLifetime.Singleton);
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
}
