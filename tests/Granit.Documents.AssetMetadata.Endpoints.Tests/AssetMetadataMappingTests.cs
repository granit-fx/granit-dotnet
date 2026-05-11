using System;
using System.Collections.Generic;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Endpoints.Dtos;
using Granit.Documents.AssetMetadata.Endpoints.Mapping;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Endpoints.Tests;

public sealed class AssetMetadataMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToResponse_should_copy_typed_columns_and_raw_metadata()
    {
        var m = DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "image/jpeg", Now);
        m.MarkExtracting();
        m.ApplyExtraction(new AssetMetadataResult(
            "exif",
            new Dictionary<string, string?>
            {
                ["Make"] = "Canon",
                ["GpsLatitude"] = "48.8566",
            })
        {
            Width = 4000,
            Height = 3000,
            CameraMake = "Canon",
            CameraModel = "EOS R5",
            Iso = 200,
            FNumber = 2.8,
            GpsLatitude = 48.8566,
            GpsLongitude = 2.3522,
            TakenAt = Now,
        });
        m.MarkReady(Now.AddSeconds(1));

        AssetMetadataResponse dto = m.ToResponse();

        dto.Id.ShouldBe(m.Id);
        dto.DocumentId.ShouldBe(m.DocumentId);
        dto.DocumentVersionId.ShouldBe(m.DocumentVersionId);
        dto.SourceContentType.ShouldBe("image/jpeg");
        dto.Status.ShouldBe(AssetMetadataStatus.Ready);
        dto.CreatedAt.ShouldBe(Now);
        dto.CompletedAt.ShouldBe(Now.AddSeconds(1));
        dto.FailureReason.ShouldBeNull();
        dto.ExtractorCount.ShouldBe(1);
        dto.Width.ShouldBe(4000);
        dto.Height.ShouldBe(3000);
        dto.CameraMake.ShouldBe("Canon");
        dto.CameraModel.ShouldBe("EOS R5");
        dto.Iso.ShouldBe(200);
        dto.FNumber.ShouldBe(2.8);
        dto.GpsLatitude.ShouldBe(48.8566);
        dto.GpsLongitude.ShouldBe(2.3522);
        dto.TakenAt.ShouldBe(Now);
        dto.RawMetadata.ShouldContainKeyAndValue("exif:Make", "Canon");
        dto.RawMetadata.ShouldContainKeyAndValue("exif:GpsLatitude", "48.8566");
    }

    [Fact]
    public void ToResponse_should_preserve_failure_state()
    {
        var m = DocumentAssetMetadata.Create(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "application/pdf", Now);
        m.MarkExtracting();
        m.MarkFailed("boom", Now.AddSeconds(3));

        AssetMetadataResponse dto = m.ToResponse();

        dto.Status.ShouldBe(AssetMetadataStatus.Failed);
        dto.FailureReason.ShouldBe("boom");
        dto.CompletedAt.ShouldBe(Now.AddSeconds(3));
        dto.ExtractorCount.ShouldBe(0);
        dto.Width.ShouldBeNull();
        dto.RawMetadata.ShouldBeEmpty();
    }
}
