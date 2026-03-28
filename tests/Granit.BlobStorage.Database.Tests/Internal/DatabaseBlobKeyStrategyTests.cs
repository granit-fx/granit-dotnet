using Granit.BlobStorage.Database.Internal;
using Granit.MultiTenancy;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Internal;

public sealed class DatabaseBlobKeyStrategyTests
{
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public DatabaseBlobKeyStrategyTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _currentTenant.IsAvailable.Returns(false);
    }

    [Fact]
    public void ResolveBucketName_AlwaysReturnsDbstore() =>
        new DatabaseBlobKeyStrategy(_currentTenant, _clock)
            .ResolveBucketName("any-container")
            .ShouldBe("dbstore");

    [Theory]
    [InlineData("documents")]
    [InlineData("images")]
    [InlineData("reports")]
    public void ResolveBucketName_IgnoresContainerName(string containerName) =>
        new DatabaseBlobKeyStrategy(_currentTenant, _clock)
            .ResolveBucketName(containerName)
            .ShouldBe("dbstore");
}
