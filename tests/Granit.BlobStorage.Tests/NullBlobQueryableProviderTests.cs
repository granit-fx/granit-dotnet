using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Internal;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class NullBlobQueryableProviderTests
{
    [Fact]
    public void GetDescriptors_ReturnsEmptyQueryable()
    {
        NullBlobQueryableProvider provider = new();

        IQueryable<BlobDescriptor> result = provider.GetDescriptors();

        result.ShouldNotBeNull();
        result.Count().ShouldBe(0);
    }

    [Fact]
    public void GetDescriptors_ImplementsIBlobQueryableProvider()
    {
        NullBlobQueryableProvider provider = new();

        provider.ShouldBeAssignableTo<IBlobQueryableProvider>();
    }
}
