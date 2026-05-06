using Granit.Taxonomy.Extensions;
using Granit.Taxonomy.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Extensions;

public sealed class AddTaggableEntityTests
{
    private sealed class FakeAggregateA;
    private sealed class FakeAggregateB;

    [Fact]
    public void AddTaggableEntity_RegistersAggregate_TypeFullNameIsTargetType()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitTaxonomy();
        services.AddTaggableEntity<FakeAggregateA>("documents");

        using ServiceProvider sp = services.BuildServiceProvider();
        TaggableTypeRegistry registry = sp.GetRequiredService<TaggableTypeRegistry>();

        registry.IsRegistered(typeof(FakeAggregateA).FullName!).ShouldBeTrue();
        registry.GetScope(typeof(FakeAggregateA).FullName!).ShouldBe("documents");
    }

    [Fact]
    public void AddTaggableEntity_DifferentTypes_BothRegistered()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitTaxonomy();
        services.AddTaggableEntity<FakeAggregateA>("documents");
        services.AddTaggableEntity<FakeAggregateB>("parties");

        using ServiceProvider sp = services.BuildServiceProvider();
        TaggableTypeRegistry registry = sp.GetRequiredService<TaggableTypeRegistry>();

        registry.GetScope(typeof(FakeAggregateA).FullName!).ShouldBe("documents");
        registry.GetScope(typeof(FakeAggregateB).FullName!).ShouldBe("parties");
    }

    [Fact]
    public void AddTaggableEntity_SameTypeDifferentScope_RegistryFactoryThrows()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitTaxonomy();
        services.AddTaggableEntity<FakeAggregateA>("documents");
        services.AddTaggableEntity<FakeAggregateA>("parties");

        using ServiceProvider sp = services.BuildServiceProvider();

        // The registry factory invokes Register() per markers; the second one with a
        // different scope must throw to prevent ambiguous (TargetType -> Scope) state.
        Should.Throw<InvalidOperationException>(() => sp.GetRequiredService<TaggableTypeRegistry>());
    }
}
