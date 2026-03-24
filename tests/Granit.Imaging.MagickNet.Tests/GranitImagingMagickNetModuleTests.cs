using Granit.Imaging.MagickNet.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void AddGranitImagingMagickNet_RegistersIImageProcessor()
    {
        ServiceCollection services = new();
        services.AddGranitImagingMagickNet();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IImageProcessor) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitImagingMagickNet_IsTryAdd_SecondCallDoesNotDuplicate()
    {
        ServiceCollection services = new();
        services.AddGranitImagingMagickNet();
        services.AddGranitImagingMagickNet();

        int count = services.Count(d => d.ServiceType == typeof(IImageProcessor));
        count.ShouldBe(1);
    }

    [Fact]
    public void AddGranitImagingMagickNet_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitImagingMagickNet();

        result.ShouldBeSameAs(services);
    }
}
