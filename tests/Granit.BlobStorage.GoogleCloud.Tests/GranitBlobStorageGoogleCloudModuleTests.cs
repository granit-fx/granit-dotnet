using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.GoogleCloud.Tests;

public sealed class GranitBlobStorageGoogleCloudModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitBlobStorageGoogleCloudModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitBlobStorageGoogleCloudModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_GranitBlobStorageModule()
    {
        Type[] dependencies = typeof(GranitBlobStorageGoogleCloudModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        dependencies.ShouldContain(typeof(GranitBlobStorageModule));
    }
}
