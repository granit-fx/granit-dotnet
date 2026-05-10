using System;
using System.Collections.Generic;
using Granit.BlobStorage;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Endpoints.Dtos;
using Granit.Documents.Renditions.Endpoints.Mapping;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Endpoints.Tests;

public sealed class RenditionMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToResponse_should_copy_every_field()
    {
        var r = DocumentRendition.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            RenditionType.Thumbnail, "image/webp", Now);
        r.MarkGenerating();
        r.MarkReady(Guid.NewGuid(), sizeBytes: 4096, width: 200, height: 200, Now.AddSeconds(2));

        RenditionResponse dto = r.ToResponse();

        dto.Id.ShouldBe(r.Id);
        dto.DocumentId.ShouldBe(r.DocumentId);
        dto.DocumentVersionId.ShouldBe(r.DocumentVersionId);
        dto.Type.ShouldBe(RenditionType.Thumbnail);
        dto.Format.ShouldBe("image/webp");
        dto.Status.ShouldBe(RenditionStatus.Ready);
        dto.SizeBytes.ShouldBe(4096);
        dto.Width.ShouldBe(200);
        dto.Height.ShouldBe(200);
        dto.CreatedAt.ShouldBe(Now);
        dto.CompletedAt.ShouldBe(Now.AddSeconds(2));
        dto.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void ToListResponse_should_preserve_order_and_set_metadata()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        IReadOnlyList<DocumentRendition> rows =
        [
            DocumentRendition.CreatePending(Guid.NewGuid(), null, documentId, versionId, RenditionType.Thumbnail, "image/png", Now),
            DocumentRendition.CreatePending(Guid.NewGuid(), null, documentId, versionId, RenditionType.Web, "image/webp", Now),
        ];

        ListRenditionsResponse dto = rows.ToListResponse(documentId, versionId);

        dto.DocumentId.ShouldBe(documentId);
        dto.DocumentVersionId.ShouldBe(versionId);
        dto.Renditions.Count.ShouldBe(2);
        dto.Renditions[0].Type.ShouldBe(RenditionType.Thumbnail);
        dto.Renditions[1].Type.ShouldBe(RenditionType.Web);
    }

    [Fact]
    public void ToResponse_should_map_PresignedDownloadUrl()
    {
        var url = new Uri("https://blobs.example/render/abc");
        DateTimeOffset expires = Now.AddMinutes(5);
        RenditionDownloadUrlResponse dto = new PresignedDownloadUrl(url, expires).ToResponse();
        dto.Url.ShouldBe(url);
        dto.ExpiresAt.ShouldBe(expires);
    }
}
