using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests;

public sealed class GranitImagingMagickNetModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitImagingModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitImagingMagickNetModule), typeof(DependsOnAttribute));

        attributes.SelectMany(a => a.DependedTypes)
                  .ShouldContain(typeof(GranitImagingModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitImagingMagickNetModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFrom_GranitModule() =>
        typeof(GranitImagingMagickNetModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
}
