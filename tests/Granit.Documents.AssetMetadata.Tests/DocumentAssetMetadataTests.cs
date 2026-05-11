using System;
using System.Collections.Generic;
using System.Linq;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Events;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Tests;

public sealed class DocumentAssetMetadataTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 14, 0, 0, TimeSpan.Zero);

    private static DocumentAssetMetadata NewPending(string mime = "image/jpeg") =>
        DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            mime, Now);

    [Fact]
    public void Create_initialises_status_and_typed_columns_as_null()
    {
        DocumentAssetMetadata m = NewPending();

        m.Status.ShouldBe(AssetMetadataStatus.Pending);
        m.SourceContentType.ShouldBe("image/jpeg");
        m.Width.ShouldBeNull();
        m.GpsLatitude.ShouldBeNull();
        m.RawMetadata.Count.ShouldBe(0);
        m.ExtractorCount.ShouldBe(0);
    }

    [Fact]
    public void Create_rejects_empty_content_type() =>
        Should.Throw<ArgumentException>(() => DocumentAssetMetadata.Create(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "", Now));

    [Fact]
    public void MarkExtracting_throws_from_Ready()
    {
        DocumentAssetMetadata m = NewPending();
        m.MarkExtracting();
        m.MarkReady(Now);

        Should.Throw<InvalidOperationException>(() => m.MarkExtracting());
    }

    [Fact]
    public void ApplyExtraction_first_write_wins_on_typed_columns()
    {
        DocumentAssetMetadata m = NewPending();
        m.MarkExtracting();

        m.ApplyExtraction(new AssetMetadataResult(
            "exif",
            new Dictionary<string, string?> { ["Make"] = "Canon" })
        {
            Width = 4000,
            CameraMake = "Canon",
            GpsLatitude = 48.8566,
        });

        m.ApplyExtraction(new AssetMetadataResult(
            "xmp",
            new Dictionary<string, string?> { ["Lens"] = "EF 50mm" })
        {
            Width = 1234, // ignored — first-write-wins
            LensModel = "EF 50mm",
        });

        m.Width.ShouldBe(4000);
        m.CameraMake.ShouldBe("Canon");
        m.GpsLatitude.ShouldBe(48.8566);
        m.LensModel.ShouldBe("EF 50mm");
        m.ExtractorCount.ShouldBe(2);
        m.RawMetadata["exif:Make"].ShouldBe("Canon");
        m.RawMetadata["xmp:Lens"].ShouldBe("EF 50mm");
    }

    [Fact]
    public void StripGps_clears_typed_and_raw_gps_entries()
    {
        DocumentAssetMetadata m = NewPending();
        m.MarkExtracting();
        m.ApplyExtraction(new AssetMetadataResult(
            "exif",
            new Dictionary<string, string?>
            {
                ["Make"] = "Canon",
                ["GpsLatitude"] = "48.8566",
                ["GpsLongitude"] = "2.3522",
            })
        {
            GpsLatitude = 48.8566,
            GpsLongitude = 2.3522,
            CameraMake = "Canon",
        });

        m.StripGps();

        m.GpsLatitude.ShouldBeNull();
        m.GpsLongitude.ShouldBeNull();
        m.GpsAltitude.ShouldBeNull();
        m.CameraMake.ShouldBe("Canon");
        m.RawMetadata.ShouldContainKey("exif:Make");
        m.RawMetadata.ShouldNotContainKey("exif:GpsLatitude");
        m.RawMetadata.ShouldNotContainKey("exif:GpsLongitude");
    }

    [Fact]
    public void MarkReady_emits_AssetMetadataExtractedEvent()
    {
        DocumentAssetMetadata m = NewPending();
        m.MarkExtracting();
        m.ApplyExtraction(new AssetMetadataResult(
            "exif", new Dictionary<string, string?> { ["a"] = "b" }));

        m.MarkReady(Now.AddSeconds(2));

        m.Status.ShouldBe(AssetMetadataStatus.Ready);
        m.CompletedAt.ShouldBe(Now.AddSeconds(2));
        AssetMetadataExtractedEvent evt = m.DomainEvents
            .OfType<AssetMetadataExtractedEvent>().ShouldHaveSingleItem();
        evt.ExtractorCount.ShouldBe(1);
        evt.SourceContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public void MarkReady_throws_from_Pending()
    {
        DocumentAssetMetadata m = NewPending();
        Should.Throw<InvalidOperationException>(() => m.MarkReady(Now));
    }

    [Fact]
    public void MarkFailed_emits_event_and_can_be_retried_via_MarkExtracting()
    {
        DocumentAssetMetadata m = NewPending();
        m.MarkExtracting();
        m.MarkFailed("provider exploded", Now);

        m.Status.ShouldBe(AssetMetadataStatus.Failed);
        m.FailureReason.ShouldBe("provider exploded");
        m.DomainEvents.OfType<AssetMetadataFailedEvent>().ShouldHaveSingleItem();

        m.MarkExtracting();
        m.Status.ShouldBe(AssetMetadataStatus.Extracting);
        m.FailureReason.ShouldBeNull();
    }
}
