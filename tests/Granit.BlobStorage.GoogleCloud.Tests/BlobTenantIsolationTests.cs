using Granit.BlobStorage.GoogleCloud;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.GoogleCloud.Tests;

public sealed class BlobTenantIsolationTests
{
    [Fact]
    public void Prefix_HasValueZero() =>
        ((int)BlobTenantIsolation.Prefix).ShouldBe(0);

    [Fact]
    public void Bucket_HasValueOne() =>
        ((int)BlobTenantIsolation.Bucket).ShouldBe(1);

    [Fact]
    public void Enum_HasTwoValues() =>
        Enum.GetValues<BlobTenantIsolation>().Length.ShouldBe(2);
}
