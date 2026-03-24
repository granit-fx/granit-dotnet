// =============================================================================
// Tests - ModuleLoader
// =============================================================================
// Verifies that ModuleLoader:
//   - Loads a single module without dependencies
//   - Respects topological order (linear chain, diamond)
//   - Detects and rejects circular dependencies
//   - Rejects types that are not GranitModule
//   - Deduplicates duplicate declared dependencies
// =============================================================================

using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class ModuleLoaderTests
{
    // --- Test modules ---

    public sealed class StandaloneModule : GranitModule;

    [DependsOn(typeof(StandaloneModule))]
    public sealed class DependentModule : GranitModule;

    // Linear chain: C → B → A (A is standalone)
    public sealed class ModuleA : GranitModule;

    [DependsOn(typeof(ModuleA))]
    public sealed class ModuleB : GranitModule;

    [DependsOn(typeof(ModuleB))]
    public sealed class ModuleC : GranitModule;

    // Diamond: Root → Left + Right, Left → Shared, Right → Shared
    public sealed class SharedModule : GranitModule;

    [DependsOn(typeof(SharedModule))]
    public sealed class LeftModule : GranitModule;

    [DependsOn(typeof(SharedModule))]
    public sealed class RightModule : GranitModule;

    [DependsOn(typeof(LeftModule), typeof(RightModule))]
    public sealed class DiamondRootModule : GranitModule;

    // Circular dependency
    [DependsOn(typeof(CircularB))]
    public sealed class CircularA : GranitModule;

    [DependsOn(typeof(CircularA))]
    public sealed class CircularB : GranitModule;

    // Duplicate dependency
    [DependsOn(typeof(StandaloneModule))]
    [DependsOn(typeof(StandaloneModule))]
    public sealed class DuplicateDepsModule : GranitModule;

    // Not a module
    public sealed class NotAModule;

    // --- Tests ---

    [Fact]
    public void LoadModules_SingleModule_ReturnsOnlyThatModule()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<StandaloneModule>();

        modules.Count.ShouldBe(1);
        modules[0].ModuleType.ShouldBe(typeof(StandaloneModule));
        modules[0].Instance.ShouldBeOfType<StandaloneModule>();
    }

    [Fact]
    public void LoadModules_LinearChain_ReturnsDependenciesFirst()
    {
        // C → B → A : expected order A, B, C
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<ModuleC>();

        modules.Count.ShouldBe(3);
        var types = modules.Select(m => m.ModuleType).ToList();
        types.IndexOf(typeof(ModuleA)).ShouldBeLessThan(types.IndexOf(typeof(ModuleB)));
        types.IndexOf(typeof(ModuleB)).ShouldBeLessThan(types.IndexOf(typeof(ModuleC)));
    }

    [Fact]
    public void LoadModules_Diamond_SharedLoadedOnce()
    {
        // DiamondRoot → Left + Right → Shared

        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DiamondRootModule>();

        modules.Count.ShouldBe(4);

        // Shared must appear exactly once
        modules.Where(m => m.ModuleType == typeof(SharedModule)).Count().ShouldBe(1);

        // Shared must come before Left and Right
        var types = modules.Select(m => m.ModuleType).ToList();
        types.IndexOf(typeof(SharedModule)).ShouldBeLessThan(types.IndexOf(typeof(LeftModule)));
        types.IndexOf(typeof(SharedModule)).ShouldBeLessThan(types.IndexOf(typeof(RightModule)));

        // Left and Right must come before DiamondRoot
        types.IndexOf(typeof(LeftModule)).ShouldBeLessThan(types.IndexOf(typeof(DiamondRootModule)));
        types.IndexOf(typeof(RightModule)).ShouldBeLessThan(types.IndexOf(typeof(DiamondRootModule)));
    }

    [Fact]
    public void LoadModules_CircularDependency_ThrowsInvalidOperationException()
    {
        Action act = () => ModuleLoader.LoadModules<CircularA>();

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("Circular dependency");
    }

    [Fact]
    public void LoadModules_NonModuleType_ThrowsInvalidOperationException()
    {
        Action act = () => ModuleLoader.LoadModules(typeof(NotAModule));

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("GranitModule");
    }

    [Fact]
    public void LoadModules_DuplicateDependency_DeduplicatesCorrectly()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DuplicateDepsModule>();

        modules.Count.ShouldBe(2);
        modules.Where(m => m.ModuleType == typeof(StandaloneModule)).Count().ShouldBe(1);
    }

    [Fact]
    public void LoadModules_WithDependsOn_SetsCorrectDependencies()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependentModule>();

        ModuleDescriptor dependent = modules.Single(m => m.ModuleType == typeof(DependentModule));
        dependent.Dependencies.ShouldContain(typeof(StandaloneModule));
    }
}
