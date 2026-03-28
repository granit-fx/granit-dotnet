using Granit.BlobStorage.Database.Options;
using Granit.BlobStorage.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Options;

public sealed class DatabaseBlobOptionsTests
{
    [Fact]
    public void MaxBlobSizeBytes_DefaultsTo10MB() =>
        new DatabaseBlobOptions().MaxBlobSizeBytes.ShouldBe(10 * 1024 * 1024);

    [Fact]
    public void SectionName_InheritsFromBlobStorageOptions() =>
        BlobStorageOptions.SectionName.ShouldBe("BlobStorage");

    [Fact]
    public void MaxBlobSizeBytes_CanBeOverridden()
    {
        DatabaseBlobOptions options = new() { MaxBlobSizeBytes = 5 * 1024 * 1024 };
        options.MaxBlobSizeBytes.ShouldBe(5 * 1024 * 1024);
    }
}
