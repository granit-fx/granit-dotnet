using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class GranitBuilderTests
{
    [Fact]
    public void AddModule_RegistersModuleType()
    {
        GranitBuilder builder = new();

        builder.AddModule<TestModuleA>();

        builder.ModuleTypes.ShouldContain(typeof(TestModuleA));
    }

    [Fact]
    public void AddModule_DeduplicatesSameModule()
    {
        GranitBuilder builder = new();

        builder.AddModule<TestModuleA>();
        builder.AddModule<TestModuleA>();

        builder.ModuleTypes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddModule_ReturnsSameBuilderForChaining()
    {
        GranitBuilder builder = new();

        GranitBuilder result = builder.AddModule<TestModuleA>();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddModule_AcceptsMultipleModuleTypes()
    {
        GranitBuilder builder = new();

        builder.AddModule<TestModuleA>()
               .AddModule<TestModuleB>();

        builder.ModuleTypes.ShouldContain(typeof(TestModuleA));
        builder.ModuleTypes.ShouldContain(typeof(TestModuleB));
        builder.ModuleTypes.Count.ShouldBe(2);
    }

    [Fact]
    public void ModuleTypes_EmptyByDefault()
    {
        GranitBuilder builder = new();

        builder.ModuleTypes.ShouldBeEmpty();
    }

    // --- Test modules ---

    public sealed class TestModuleA : GranitModule;

    [DependsOn(typeof(TestModuleA))]
    public sealed class TestModuleB : GranitModule;
}
