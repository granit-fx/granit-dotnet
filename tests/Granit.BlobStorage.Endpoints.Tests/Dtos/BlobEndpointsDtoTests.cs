using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Dtos;

public sealed class BlobEndpointsDtoTests
{
    // ── BlobUploadInitiateRequest ────────────────────────────────────────────

    [Fact]
    public void BlobUploadInitiateRequest_SetsAllProperties()
    {
        BlobUploadInitiateRequest request = new("medical-images", "scan.pdf", "application/pdf", 10_000_000L);

        request.ContainerName.ShouldBe("medical-images");
        request.FileName.ShouldBe("scan.pdf");
        request.ContentType.ShouldBe("application/pdf");
        request.SizeBytes.ShouldBe(10_000_000L);
    }

    // ── BlobUploadInitiateResponse ───────────────────────────────────────────

    [Fact]
    public void BlobUploadInitiateResponse_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        Dictionary<string, string> headers = new() { ["Content-Type"] = "application/pdf" };

        BlobUploadInitiateResponse response = new(blobId, "https://s3.example.com/upload", "PUT", expiresAt, headers);

        response.BlobId.ShouldBe(blobId);
        response.UploadUrl.ShouldBe("https://s3.example.com/upload");
        response.HttpMethod.ShouldBe("PUT");
        response.ExpiresAt.ShouldBe(expiresAt);
        response.RequiredHeaders.ShouldContainKey("Content-Type");
    }

    // ── BlobConfirmUploadRequest ─────────────────────────────────────────────

    [Fact]
    public void BlobConfirmUploadRequest_SetsContainerName()
    {
        BlobConfirmUploadRequest request = new("medical-images");

        request.ContainerName.ShouldBe("medical-images");
    }

    // ── BlobConfirmUploadResponse ────────────────────────────────────────────

    [Fact]
    public void BlobConfirmUploadResponse_ValidResult_SetsAllProperties()
    {
        var blobId = Guid.NewGuid();
        BlobConfirmUploadResponse response = new(blobId, true, BlobStatus.Valid, "application/pdf", 5_000L, null);

        response.BlobId.ShouldBe(blobId);
        response.IsValid.ShouldBeTrue();
        response.Status.ShouldBe(BlobStatus.Valid);
        response.VerifiedContentType.ShouldBe("application/pdf");
        response.SizeBytes.ShouldBe(5_000L);
        response.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void BlobConfirmUploadResponse_RejectedResult_SetsRejectionReason()
    {
        BlobConfirmUploadResponse response = new(Guid.NewGuid(), false, BlobStatus.Rejected, null, null, "File too large");

        response.IsValid.ShouldBeFalse();
        response.RejectionReason.ShouldBe("File too large");
    }

    // ── BlobDeleteRequest ────────────────────────────────────────────────────

    [Fact]
    public void BlobDeleteRequest_SetsContainerName()
    {
        BlobDeleteRequest request = new("docs");

        request.ContainerName.ShouldBe("docs");
        request.DeletionReason.ShouldBeNull();
    }

    [Fact]
    public void BlobDeleteRequest_WithReason_SetsReason()
    {
        BlobDeleteRequest request = new("docs", "GDPR Art. 17");

        request.DeletionReason.ShouldBe("GDPR Art. 17");
    }

    // ── BlobDownloadUrlRequest ───────────────────────────────────────────────

    [Fact]
    public void BlobDownloadUrlRequest_SetsContainerName()
    {
        BlobDownloadUrlRequest request = new("docs");

        request.ContainerName.ShouldBe("docs");
        request.FileName.ShouldBeNull();
    }

    [Fact]
    public void BlobDownloadUrlRequest_WithFileName_SetsFileName()
    {
        BlobDownloadUrlRequest request = new("docs", "report.pdf");

        request.FileName.ShouldBe("report.pdf");
    }

    // ── BlobDownloadUrlResponse ──────────────────────────────────────────────

    [Fact]
    public void BlobDownloadUrlResponse_SetsAllProperties()
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        BlobDownloadUrlResponse response = new("https://s3.example.com/download", expiresAt);

        response.DownloadUrl.ShouldBe("https://s3.example.com/download");
        response.ExpiresAt.ShouldBe(expiresAt);
    }

    // ── BlobCleanupOrphansResponse ───────────────────────────────────────────

    [Fact]
    public void BlobCleanupOrphansResponse_SetsCleanedCount()
    {
        BlobCleanupOrphansResponse response = new(42);

        response.CleanedCount.ShouldBe(42);
    }

    // ── BlobDescriptorResponse ───────────────────────────────────────────────

    [Fact]
    public void BlobDescriptorResponse_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset validatedAt = createdAt.AddMinutes(1);

        BlobDescriptorResponse response = new(
            id, "images", "photo.jpg", "image/jpeg", "image/jpeg",
            10_000_000L, 8_500_000L, BlobStatus.Valid,
            null, null, createdAt, validatedAt, null);

        response.Id.ShouldBe(id);
        response.ContainerName.ShouldBe("images");
        response.OriginalFileName.ShouldBe("photo.jpg");
        response.DeclaredContentType.ShouldBe("image/jpeg");
        response.VerifiedContentType.ShouldBe("image/jpeg");
        response.DeclaredSizeBytes.ShouldBe(10_000_000L);
        response.ActualSizeBytes.ShouldBe(8_500_000L);
        response.Status.ShouldBe(BlobStatus.Valid);
        response.RejectionReason.ShouldBeNull();
        response.DeletionReason.ShouldBeNull();
        response.CreatedAt.ShouldBe(createdAt);
        response.ValidatedAt.ShouldBe(validatedAt);
        response.DeletedAt.ShouldBeNull();
    }
}
