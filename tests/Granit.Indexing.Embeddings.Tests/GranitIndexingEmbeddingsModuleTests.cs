using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Embeddings.Tests;

public sealed class GranitIndexingEmbeddingsModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitIndexingEmbeddingsModule).IsSealed.ShouldBeTrue();
        new GranitIndexingEmbeddingsModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_GranitIndexingModule_only()
    {
        // The storage backends are wired by host opt-in extensions (EF / ES) — module
        // discovery MUST NOT couple this package to either backend. Locking the
        // dependency set keeps the embeddings opt-in even on hosts that scan modules.
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitIndexingEmbeddingsModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitIndexingModule));
        attr.DependedTypes.Length.ShouldBe(1);
    }
}
