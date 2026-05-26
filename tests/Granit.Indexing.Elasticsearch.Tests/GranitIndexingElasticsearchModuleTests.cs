using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests;

public sealed class GranitIndexingElasticsearchModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitIndexingElasticsearchModule).IsSealed.ShouldBeTrue();
        new GranitIndexingElasticsearchModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_GranitIndexingModule()
    {
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitIndexingElasticsearchModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitIndexingModule));
    }
}
