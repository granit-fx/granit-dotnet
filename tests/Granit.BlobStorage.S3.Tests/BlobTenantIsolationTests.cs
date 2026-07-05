using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

public sealed class BlobTenantIsolationTests
{
    [Fact]
    public void Prefix_HasExpectedValue() => ((int)BlobTenantIsolation.Prefix).ShouldBe(0);

    [Fact]
    public void Bucket_HasExpectedValue() => ((int)BlobTenantIsolation.Bucket).ShouldBe(1);

    [Fact]
    public void EnumValues_HasTwoMembers() => Enum.GetValues<BlobTenantIsolation>().Length.ShouldBe(2);
}
