using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Indexing.AI.Tests;

public sealed class GranitIndexingAIModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitIndexingAIModule).IsSealed.ShouldBeTrue();
        new GranitIndexingAIModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_AI_and_Indexing()
    {
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitIndexingAIModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(Granit.AI.GranitAIModule));
        attr.DependedTypes.ShouldContain(typeof(GranitIndexingModule));
    }
}
