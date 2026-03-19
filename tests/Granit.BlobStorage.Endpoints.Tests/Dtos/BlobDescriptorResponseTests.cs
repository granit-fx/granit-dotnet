using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Dtos;

public sealed class BlobDescriptorResponseTests
{
    [Fact]
    public void Record_should_hold_all_properties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        BlobDescriptorResponse response = new(
            id,
            "medical-images",
            "radio.jpg",
            "image/jpeg",
            "image/jpeg",
            10_000_000,
            512_000,
            BlobStatus.Valid,
            null,
            null,
            now,
            now.AddSeconds(5),
            null);

        response.Id.ShouldBe(id);
        response.ContainerName.ShouldBe("medical-images");
        response.OriginalFileName.ShouldBe("radio.jpg");
        response.Status.ShouldBe(BlobStatus.Valid);
        response.ActualSizeBytes.ShouldBe(512_000);
        response.VerifiedContentType.ShouldBe("image/jpeg");
        response.RejectionReason.ShouldBeNull();
    }
}
