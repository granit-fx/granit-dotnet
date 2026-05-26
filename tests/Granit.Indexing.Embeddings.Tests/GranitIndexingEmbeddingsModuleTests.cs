using Granit.AI;
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
    public void Module_declares_DependsOn_GranitIndexingModule_and_GranitAIModule()
    {
        // Storage backends are wired by host opt-in extensions (EF / ES) — module
        // discovery MUST NOT couple this package to either backend. Locking the
        // dependency set to {Indexing, AI} keeps the storage path opt-in while
        // making the workspace-routed factory available without extra wiring.
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitIndexingEmbeddingsModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitIndexingModule));
        attr.DependedTypes.ShouldContain(typeof(GranitAIModule));
        attr.DependedTypes.Length.ShouldBe(2);
    }
}
