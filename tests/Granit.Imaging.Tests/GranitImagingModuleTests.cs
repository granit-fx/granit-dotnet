using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Imaging.Tests;

public sealed class GranitImagingModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitImagingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFrom_GranitModule() => typeof(GranitImagingModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_CanBeInstantiated()
    {
        GranitImagingModule module = new();

        module.ShouldNotBeNull();
    }

    [Fact]
    public void Module_HasNoDependsOnAttributes()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitImagingModule), typeof(DependsOnAttribute));

        attributes.ShouldBeEmpty();
    }
}
