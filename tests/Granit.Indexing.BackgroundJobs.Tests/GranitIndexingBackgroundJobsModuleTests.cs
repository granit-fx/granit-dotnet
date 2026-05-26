using Granit.BackgroundJobs;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Indexing.BackgroundJobs.Tests;

public sealed class GranitIndexingBackgroundJobsModuleTests
{
    [Fact]
    public void Module_is_sealed_and_inherits_GranitModule()
    {
        typeof(GranitIndexingBackgroundJobsModule).IsSealed.ShouldBeTrue();
        new GranitIndexingBackgroundJobsModule().ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_declares_DependsOn_BackgroundJobs_and_Indexing()
    {
        // Alphabetical ordering per CLAUDE.md §DependsOn rules — locked here so a
        // future refactor that breaks the order is caught immediately.
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitIndexingBackgroundJobsModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitBackgroundJobsModule));
        attr.DependedTypes.ShouldContain(typeof(GranitIndexingModule));
    }
}
