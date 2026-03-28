using Granit.BlobStorage.Database.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Options;

public sealed class DbStoreBlobOptionsTests
{
    [Fact]
    public void MaxBlobSizeBytes_DefaultsTo10MB() =>
        new DbStoreBlobOptions().MaxBlobSizeBytes.ShouldBe(10 * 1024 * 1024);

    [Fact]
    public void MaxBlobSizeBytes_CanBeOverridden()
    {
        DbStoreBlobOptions options = new() { MaxBlobSizeBytes = 20 * 1024 * 1024 };
        options.MaxBlobSizeBytes.ShouldBe(20 * 1024 * 1024);
    }
}
