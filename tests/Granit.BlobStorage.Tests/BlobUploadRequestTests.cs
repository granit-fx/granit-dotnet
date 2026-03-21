using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobUploadRequestTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        BlobUploadRequest request = new("report.pdf", "application/pdf", 10_000_000L);

        request.FileName.ShouldBe("report.pdf");
        request.ContentType.ShouldBe("application/pdf");
        request.MaxAllowedBytes.ShouldBe(10_000_000L);
        request.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMetadata_SetsMetadata()
    {
        Dictionary<string, string> metadata = new()
        {
            ["department"] = "radiology",
            ["patient-id"] = "anonymized-123",
        };

        BlobUploadRequest request = new("scan.dcm", "application/dicom", 50_000_000L, metadata);

        request.Metadata.ShouldNotBeNull();
        request.Metadata!.Count.ShouldBe(2);
        request.Metadata["department"].ShouldBe("radiology");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        BlobUploadRequest a = new("file.pdf", "application/pdf", 5_000_000L);
        BlobUploadRequest b = new("file.pdf", "application/pdf", 5_000_000L);

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        BlobUploadRequest a = new("file.pdf", "application/pdf", 5_000_000L);
        BlobUploadRequest b = new("other.pdf", "application/pdf", 5_000_000L);

        a.ShouldNotBe(b);
    }
}
