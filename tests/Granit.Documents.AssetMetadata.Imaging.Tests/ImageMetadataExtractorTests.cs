using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Imaging.Internal;
using Granit.Documents.AssetMetadata.Options;
using ImageMagick;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Imaging.Tests;

public sealed class ImageMetadataExtractorTests
{
    private static ImageMetadataExtractor Build(bool stripGps = false) =>
        new(Microsoft.Extensions.Options.Options.Create(
            new GranitAssetMetadataOptions { StripGpsOnUpload = stripGps }));

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("application/pdf", false)]
    [InlineData("", false)]
    public void CanHandle_matches_image_family(string mime, bool expected) =>
        Build().CanHandle(mime).ShouldBe(expected);

    [Fact]
    public async Task Extract_JPEG_with_EXIF_projects_typed_columns_and_dumps_raw()
    {
        byte[] jpeg = CreateJpegWithExif(
            width: 800, height: 600,
            make: "Canon", model: "EOS R5", iso: 200,
            takenAt: new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc));

        await using MemoryStream stream = new(jpeg);
        AssetMetadataResult result = await Build()
            .ExtractAsync(stream, "image/jpeg", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("image");
        result.Width.ShouldBe(800);
        result.Height.ShouldBe(600);
        result.CameraMake.ShouldBe("Canon");
        result.CameraModel.ShouldBe("EOS R5");
        result.Iso.ShouldBe(200);
        result.TakenAt.ShouldNotBeNull();
        result.RawMetadata.ShouldContainKey("exif:Make");
        result.RawMetadata.ShouldContainKey("exif:Model");
    }

    [Fact]
    public async Task Extract_strips_GPS_when_option_enabled()
    {
        byte[] jpeg = CreateJpegWithGps(latitude: 48.8566, longitude: 2.3522);

        await using MemoryStream stream = new(jpeg);
        AssetMetadataResult stripped = await Build(stripGps: true)
            .ExtractAsync(stream, "image/jpeg", TestContext.Current.CancellationToken);

        stripped.GpsLatitude.ShouldBeNull();
        stripped.GpsLongitude.ShouldBeNull();
        stripped.GpsAltitude.ShouldBeNull();
        foreach (string key in stripped.RawMetadata.Keys)
        {
            key.ShouldNotContain("GPS", Case.Insensitive);
        }
    }

    [Fact]
    public async Task Extract_PNG_without_EXIF_returns_dimensions_only()
    {
        byte[] png = CreatePlainPng(width: 64, height: 48);

        await using MemoryStream stream = new(png);
        AssetMetadataResult result = await Build()
            .ExtractAsync(stream, "image/png", TestContext.Current.CancellationToken);

        result.Width.ShouldBe(64);
        result.Height.ShouldBe(48);
        result.CameraMake.ShouldBeNull();
        result.RawMetadata.ShouldNotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Fixture helpers — Magick.NET synthesises images with EXIF / GPS at runtime
    // so the repo never carries binary blobs.
    // -------------------------------------------------------------------------

    private static byte[] CreateJpegWithExif(
        int width, int height, string make, string model, int iso, DateTime takenAt)
    {
        using var image = new MagickImage(MagickColors.Red, (uint)width, (uint)height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, make);
        exif.SetValue(ExifTag.Model, model);
        exif.SetValue(ExifTag.ISOSpeedRatings, [(ushort)iso]);
        exif.SetValue(ExifTag.DateTimeOriginal, takenAt.ToString("yyyy:MM:dd HH:mm:ss"));
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        using MemoryStream ms = new();
        image.Write(ms);
        return ms.ToArray();
    }

    private static byte[] CreateJpegWithGps(double latitude, double longitude)
    {
        using var image = new MagickImage(MagickColors.Blue, 32, 32);
        var exif = new ExifProfile();
        Rational[] lat = ToDms(Math.Abs(latitude));
        Rational[] lon = ToDms(Math.Abs(longitude));
        exif.SetValue(ExifTag.GPSLatitudeRef, latitude >= 0 ? "N" : "S");
        exif.SetValue(ExifTag.GPSLatitude, lat);
        exif.SetValue(ExifTag.GPSLongitudeRef, longitude >= 0 ? "E" : "W");
        exif.SetValue(ExifTag.GPSLongitude, lon);
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        using MemoryStream ms = new();
        image.Write(ms);
        return ms.ToArray();
    }

    private static Rational[] ToDms(double value)
    {
        int deg = (int)Math.Floor(value);
        double rem = (value - deg) * 60;
        int min = (int)Math.Floor(rem);
        double sec = (rem - min) * 60;
        return
        [
            new((uint)deg, 1u),
            new((uint)min, 1u),
            new((uint)Math.Round(sec * 1000), 1000u),
        ];
    }

    private static byte[] CreatePlainPng(int width, int height)
    {
        using var image = new MagickImage(MagickColors.Green, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        using MemoryStream ms = new();
        image.Write(ms);
        return ms.ToArray();
    }
}
