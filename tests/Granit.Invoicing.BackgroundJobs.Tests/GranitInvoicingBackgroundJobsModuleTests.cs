using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.BackgroundJobs.Tests;

public sealed class GranitInvoicingBackgroundJobsModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed() =>
        typeof(GranitInvoicingBackgroundJobsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_ShouldInheritFromGranitModule() =>
        typeof(GranitInvoicingBackgroundJobsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitInvoicingBackgroundJobsModule();
        module.ShouldNotBeNull();
    }
}
