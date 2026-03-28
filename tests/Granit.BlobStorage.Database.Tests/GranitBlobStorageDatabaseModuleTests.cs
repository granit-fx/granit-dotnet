using Granit.BlobStorage;
using Granit.BlobStorage.Database;
using Granit.Modularity;
using Granit.Persistence;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests;

public sealed class GranitBlobStorageDatabaseModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitBlobStorageDatabaseModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitBlobStorageDatabaseModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOnBlobStorageAndPersistence()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitBlobStorageDatabaseModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        Type[] allDependencies = [.. attributes.SelectMany(a => a.DependedTypes)];

        allDependencies.ShouldContain(typeof(GranitBlobStorageModule));
        allDependencies.ShouldContain(typeof(GranitPersistenceModule));
    }
}
