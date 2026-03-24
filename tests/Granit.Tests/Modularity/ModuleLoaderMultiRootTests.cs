using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Tests.Modularity;

public sealed class ModuleLoaderMultiRootTests
{
    public sealed class IndependentModuleA : GranitModule;

    public sealed class IndependentModuleB : GranitModule;

    public sealed class SharedModule : GranitModule;

    [DependsOn(typeof(SharedModule))]
    public sealed class BranchA : GranitModule;

    [DependsOn(typeof(SharedModule))]
    public sealed class BranchB : GranitModule;

    // -------------------------------------------------------------------------
    // LoadModules(IEnumerable<Type>) — multiple roots
    // -------------------------------------------------------------------------

    [Fact]
    public void LoadModules_MultipleRoots_ReturnsAllModules()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(
            [typeof(IndependentModuleA), typeof(IndependentModuleB)]);

        modules.Count.ShouldBe(2);
        modules.ShouldContain(m => m.ModuleType == typeof(IndependentModuleA));
        modules.ShouldContain(m => m.ModuleType == typeof(IndependentModuleB));
    }

    [Fact]
    public void LoadModules_MultipleRoots_DeduplicatesSharedDependencies()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(
            [typeof(BranchA), typeof(BranchB)]);

        modules.Count.ShouldBe(3);
        modules.Count(m => m.ModuleType == typeof(SharedModule)).ShouldBe(1);
    }

    [Fact]
    public void LoadModules_MultipleRoots_SharedBeforeBranches()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(
            [typeof(BranchA), typeof(BranchB)]);

        var types = modules.Select(m => m.ModuleType).ToList();
        types.IndexOf(typeof(SharedModule)).ShouldBeLessThan(types.IndexOf(typeof(BranchA)));
        types.IndexOf(typeof(SharedModule)).ShouldBeLessThan(types.IndexOf(typeof(BranchB)));
    }

    [Fact]
    public void LoadModules_EmptyList_ReturnsEmpty()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules([]);

        modules.ShouldBeEmpty();
    }

    [Fact]
    public void LoadModules_DuplicateRoots_DeduplicatesCorrectly()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(
            [typeof(IndependentModuleA), typeof(IndependentModuleA)]);

        modules.Count.ShouldBe(1);
    }
}
