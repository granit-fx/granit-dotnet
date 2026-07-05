// =============================================================================
// Tests - AddEntityDefinition<T,Def>() registration helper
// =============================================================================

using Granit.Entities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void AddEntityDefinition_RegistersBoth_ConcreteBaseAndDescriptorInterface()
    {
        ServiceCollection services = new();
        services.AddEntityDefinition<TestEntity, TestEntityDefinition>();
        ServiceProvider provider = services.BuildServiceProvider();

        EntityDefinition<TestEntity> baseInstance = provider.GetRequiredService<EntityDefinition<TestEntity>>();
        IEntityDefinitionDescriptor descriptor = provider.GetRequiredService<IEntityDefinitionDescriptor>();

        baseInstance.ShouldBeOfType<TestEntityDefinition>();
        descriptor.ShouldBeSameAs(baseInstance);
    }

    [Fact]
    public void AddEntityDefinition_ProducesSingletonInstance()
    {
        ServiceCollection services = new();
        services.AddEntityDefinition<TestEntity, TestEntityDefinition>();
        ServiceProvider provider = services.BuildServiceProvider();

        EntityDefinition<TestEntity> first = provider.GetRequiredService<EntityDefinition<TestEntity>>();
        EntityDefinition<TestEntity> second = provider.GetRequiredService<EntityDefinition<TestEntity>>();
        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void GranitEntitiesAbstractionsModule_IsAGranitModule()
    {
        typeof(Granit.Modularity.GranitModule)
            .IsAssignableFrom(typeof(GranitEntitiesAbstractionsModule))
            .ShouldBeTrue();
    }

    private sealed class TestEntity
    {
        public string Title { get; set; } = "";
    }

    private sealed class TestEntityDefinition : EntityDefinition<TestEntity>
    {
        public override string Name => "Granit.Test.TestEntity";

        protected override void Configure(EntityDefinitionBuilder<TestEntity> builder) =>
            builder.Form("default", f => f.Section("a", s => s.Field(x => x.Title)));
    }
}
