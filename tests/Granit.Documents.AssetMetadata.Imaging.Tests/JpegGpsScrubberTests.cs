using System;
using System.Collections.Generic;
using System.IO;
using Granit.Documents.AssetMetadata.Imaging.Internal;
using ImageMagick;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Imaging.Tests;

public sealed class JpegGpsScrubberTests
{
    [Fact]
    public void TryScrub_JPEG_with_GPS_removes_every_GPS_tag_and_keeps_other_EXIF()
    {
        byte[] jpeg = CreateJpegWithGpsAndCamera(
            latitude: 48.8566, longitude: 2.3522,
            make: "Canon", model: "EOS R5");

        byte[]? scrubbed = JpegGpsScrubber.TryScrub(jpeg, "image/jpeg");

        scrubbed.ShouldNotBeNull();
        scrubbed.Length.ShouldBeGreaterThan(0);

        IReadOnlyList<MetadataExtractor.Directory> dirs =
            ImageMetadataReader.ReadMetadata(new MemoryStream(scrubbed));

        bool hasGpsValues = false;
        foreach (MetadataExtractor.Directory dir in dirs)
        {
            if (dir is GpsDirectory gps)
            {
                foreach (Tag tag in gps.Tags)
                {
                    // MetadataExtractor surfaces a GPS directory header tag even when the
                    // sub-IFD is fully empty; what we care about is the actual GPS data
                    // (lat / long / altitude / ref). After scrubbing, those should be gone.
                    if (tag.Name.Contains("Latitude", StringComparison.Ordinal)
                        || tag.Name.Contains("Longitude", StringComparison.Ordinal)
                        || tag.Name.Contains("Altitude", StringComparison.Ordinal))
                    {
                        hasGpsValues = true;
                    }
                }
            }
        }
        hasGpsValues.ShouldBeFalse("scrubbed JPEG must not carry any GPS coordinate tags");

        // Non-GPS EXIF survives — Make / Model intact.
        bool foundMake = false, foundModel = false;
        foreach (MetadataExtractor.Directory dir in dirs)
        {
            if (dir is ExifIfd0Directory ifd0)
            {
                string? make = ifd0.GetString(ExifDirectoryBase.TagMake);
                string? model = ifd0.GetString(ExifDirectoryBase.TagModel);
                if (string.Equals(make, "Canon", StringComparison.Ordinal))
                {
                    foundMake = true;
                }
                if (string.Equals(model, "EOS R5", StringComparison.Ordinal))
                {
                    foundModel = true;
                }
            }
        }
        foundMake.ShouldBeTrue("scrubbed JPEG must preserve EXIF Make");
        foundModel.ShouldBeTrue("scrubbed JPEG must preserve EXIF Model");
    }

    [Fact]
    public void TryScrub_JPEG_without_GPS_returns_null()
    {
        using var image = new MagickImage(MagickColors.Red, 32u, 32u);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Canon");
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        using MemoryStream ms = new();
        image.Write(ms);

        byte[]? scrubbed = JpegGpsScrubber.TryScrub(ms.ToArray(), "image/jpeg");

        scrubbed.ShouldBeNull();
    }

    [Fact]
    public void TryScrub_unrecognised_bytes_returns_null()
    {
        byte[] garbage = [1, 2, 3, 4, 5];

        byte[]? scrubbed = JpegGpsScrubber.TryScrub(garbage, "image/jpeg");

        scrubbed.ShouldBeNull();
    }

    [Fact]
    public void TryScrub_empty_input_returns_null() =>
        JpegGpsScrubber.TryScrub([], "image/jpeg").ShouldBeNull();

    private static byte[] CreateJpegWithGpsAndCamera(
        double latitude, double longitude, string make, string model)
    {
        using var image = new MagickImage(MagickColors.Blue, 64u, 64u);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, make);
        exif.SetValue(ExifTag.Model, model);
        ImageMagick.Rational[] lat = ToDms(Math.Abs(latitude));
        ImageMagick.Rational[] lon = ToDms(Math.Abs(longitude));
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

    private static ImageMagick.Rational[] ToDms(double value)
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
}
