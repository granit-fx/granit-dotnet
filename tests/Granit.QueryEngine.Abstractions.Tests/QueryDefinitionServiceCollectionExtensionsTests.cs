using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryDefinitionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryDefinition_resolves_concrete_type_through_DI()
    {
        ServiceCollection services = new();

        services.AddQueryDefinition<SampleEntity, SampleQueryDefinition>();

        using ServiceProvider sp = services.BuildServiceProvider();
        SampleQueryDefinition concrete = sp.GetRequiredService<SampleQueryDefinition>();

        concrete.ShouldNotBeNull();
    }

    [Fact]
    public void AddQueryDefinition_binds_base_and_descriptor_to_same_singleton()
    {
        ServiceCollection services = new();

        services.AddQueryDefinition<SampleEntity, SampleQueryDefinition>();

        using ServiceProvider sp = services.BuildServiceProvider();

        SampleQueryDefinition concrete = sp.GetRequiredService<SampleQueryDefinition>();
        QueryDefinition<SampleEntity> @base = sp.GetRequiredService<QueryDefinition<SampleEntity>>();
        IQueryDefinitionDescriptor descriptor = sp.GetRequiredService<IQueryDefinitionDescriptor>();

        @base.ShouldBeSameAs(concrete);
        descriptor.ShouldBeSameAs(concrete);
    }

    [Fact]
    public void AddQueryDefinition_concrete_appears_in_ServiceCollection_descriptors()
    {
        ServiceCollection services = new();

        services.AddQueryDefinition<SampleEntity, SampleQueryDefinition>();

        services.ShouldContain(d => d.ServiceType == typeof(SampleQueryDefinition));
    }

    private sealed class SampleEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SampleQueryDefinition : QueryDefinition<SampleEntity>
    {
        public override string Name => "Sample.Entities";

        protected override void Configure(QueryDefinitionBuilder<SampleEntity> builder) =>
            builder.Column(e => e.Name);
    }
}
