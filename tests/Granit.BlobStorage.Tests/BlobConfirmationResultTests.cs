using Granit.BlobStorage.Domain;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobConfirmationResultTests
{
    [Fact]
    public void ValidResult_SetsAllProperties()
    {
        BlobConfirmationResult result = new(true, BlobStatus.Valid, "application/pdf", 5_000L, null);

        result.IsValid.ShouldBeTrue();
        result.Status.ShouldBe(BlobStatus.Valid);
        result.VerifiedContentType.ShouldBe("application/pdf");
        result.SizeBytes.ShouldBe(5_000L);
        result.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void RejectedResult_SetsRejectionReason()
    {
        BlobConfirmationResult result = new(false, BlobStatus.Rejected, null, null, "Size exceeded");

        result.IsValid.ShouldBeFalse();
        result.Status.ShouldBe(BlobStatus.Rejected);
        result.VerifiedContentType.ShouldBeNull();
        result.SizeBytes.ShouldBeNull();
        result.RejectionReason.ShouldBe("Size exceeded");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        BlobConfirmationResult a = new(true, BlobStatus.Valid, "image/png", 1024L, null);
        BlobConfirmationResult b = new(true, BlobStatus.Valid, "image/png", 1024L, null);

        a.ShouldBe(b);
    }
}
