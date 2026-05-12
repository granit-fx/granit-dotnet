using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Options;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using MetadataExtractor.Formats.Xmp;
using Microsoft.Extensions.Options;

namespace Granit.Documents.AssetMetadata.Imaging.Internal;

/// <summary>
/// Image extractor built on the MetadataExtractor NuGet (Drew Noakes, Apache-2.0).
/// Handles every <c>image/*</c> source and projects EXIF / IPTC / XMP directories
/// into the typed columns of <see cref="AssetMetadataResult"/>. The full directory
/// dump is preserved verbatim in <see cref="AssetMetadataResult.RawMetadata"/>
/// under <c>exif:</c>, <c>iptc:</c>, <c>xmp:</c>, <c>jpeg:</c>, <c>png:</c>, and
/// <c>webp:</c> prefixes (the trailing colon stripped by the pipeline merge).
/// </summary>
internal sealed class ImageMetadataExtractor(
    IOptions<GranitAssetMetadataOptions> options) : IAssetMetadataExtractor
{
    private readonly GranitAssetMetadataOptions _options = options.Value;

    /// <inheritdoc />
    public string Name => "image";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType) =>
        !string.IsNullOrWhiteSpace(sourceContentType)
        && sourceContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<AssetMetadataResult> ExtractAsync(
        Stream source, string sourceContentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractCore(source, cancellationToken), cancellationToken);
    }

    private AssetMetadataResult ExtractCore(Stream source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        IReadOnlyList<MetadataExtractor.Directory> directories =
            ImageMetadataReader.ReadMetadata(source);

        var raw = new Dictionary<string, string?>(StringComparer.Ordinal);
        AssetMetadataResult projection = ProjectDirectories(directories, raw);

        if (_options.StripGpsOnUpload)
        {
            projection = projection with
            {
                GpsLatitude = null,
                GpsLongitude = null,
                GpsAltitude = null,
            };
            ScrubGps(raw);
        }

        return projection with { RawMetadata = raw };
    }

    private static AssetMetadataResult ProjectDirectories(
        IReadOnlyList<MetadataExtractor.Directory> directories,
        Dictionary<string, string?> raw)
    {
        int? width = null;
        int? height = null;
        string? make = null;
        string? model = null;
        string? lens = null;
        int? iso = null;
        double? fnumber = null;
        double? exposureMs = null;
        DateTimeOffset? takenAt = null;

        foreach (MetadataExtractor.Directory dir in directories)
        {
            string prefix = DirectoryPrefix(dir);
            foreach (Tag tag in dir.Tags)
            {
                string key = $"{prefix}:{tag.Name}";
                raw[key] = tag.Description;
            }

            switch (dir)
            {
                case ExifIfd0Directory ifd0:
                    make ??= ifd0.GetString(ExifDirectoryBase.TagMake);
                    model ??= ifd0.GetString(ExifDirectoryBase.TagModel);
                    if (ifd0.TryGetInt32(ExifDirectoryBase.TagImageWidth, out int w0))
                    {
                        width ??= w0;
                    }
                    if (ifd0.TryGetInt32(ExifDirectoryBase.TagImageHeight, out int h0))
                    {
                        height ??= h0;
                    }
                    break;

                case ExifSubIfdDirectory sub:
                    if (sub.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out int isoVal))
                    {
                        iso ??= isoVal;
                    }
                    if (sub.TryGetRational(ExifDirectoryBase.TagFNumber, out Rational fn))
                    {
                        fnumber ??= fn.ToDouble();
                    }
                    if (sub.TryGetRational(ExifDirectoryBase.TagExposureTime, out Rational exp))
                    {
                        exposureMs ??= exp.ToDouble() * 1000d;
                    }
                    if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out int wSub))
                    {
                        width ??= wSub;
                    }
                    if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out int hSub))
                    {
                        height ??= hSub;
                    }
                    if (sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime taken))
                    {
                        takenAt ??= new DateTimeOffset(DateTime.SpecifyKind(taken, DateTimeKind.Utc));
                    }
                    lens ??= sub.GetString(ExifDirectoryBase.TagLensModel);
                    break;

                case JpegDirectory jpeg:
                    if (jpeg.TryGetInt32(JpegDirectory.TagImageWidth, out int wJ))
                    {
                        width ??= wJ;
                    }
                    if (jpeg.TryGetInt32(JpegDirectory.TagImageHeight, out int hJ))
                    {
                        height ??= hJ;
                    }
                    break;

                case PngDirectory png:
                    if (png.TryGetInt32(PngDirectory.TagImageWidth, out int wP))
                    {
                        width ??= wP;
                    }
                    if (png.TryGetInt32(PngDirectory.TagImageHeight, out int hP))
                    {
                        height ??= hP;
                    }
                    break;

                case WebPDirectory webp:
                    if (webp.TryGetInt32(WebPDirectory.TagImageWidth, out int wW))
                    {
                        width ??= wW;
                    }
                    if (webp.TryGetInt32(WebPDirectory.TagImageHeight, out int hW))
                    {
                        height ??= hW;
                    }
                    break;
            }
        }

        (double? lat, double? lon, double? alt) gps = ReadGps(directories);

        return new AssetMetadataResult("image", new Dictionary<string, string?>(StringComparer.Ordinal))
        {
            Width = width,
            Height = height,
            CameraMake = NullIfEmpty(make),
            CameraModel = NullIfEmpty(model),
            LensModel = NullIfEmpty(lens),
            Iso = iso,
            FNumber = fnumber,
            ExposureTimeMs = exposureMs,
            TakenAt = takenAt,
            GpsLatitude = gps.lat,
            GpsLongitude = gps.lon,
            GpsAltitude = gps.alt,
        };
    }

    private static (double? Lat, double? Lon, double? Alt) ReadGps(
        IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (MetadataExtractor.Directory dir in directories)
        {
            if (dir is not GpsDirectory gps)
            {
                continue;
            }
            GeoLocation? location = gps.GetGeoLocation();
            double? altitude = null;
            if (gps.TryGetRational(GpsDirectory.TagAltitude, out Rational alt))
            {
                altitude = alt.ToDouble();
            }
            return (location?.Latitude, location?.Longitude, altitude);
        }
        return (null, null, null);
    }

    private static string DirectoryPrefix(MetadataExtractor.Directory dir) => dir switch
    {
        ExifIfd0Directory or ExifSubIfdDirectory or ExifThumbnailDirectory
            or GpsDirectory or ExifInteropDirectory => "exif",
        IptcDirectory => "iptc",
        XmpDirectory => "xmp",
        JpegDirectory => "jpeg",
        PngDirectory => "png",
        WebPDirectory => "webp",
        _ => dir.Name.ToLowerInvariant().Replace(' ', '_'),
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ScrubGps(Dictionary<string, string?> raw)
    {
        List<string> toRemove = [];
        foreach (string key in raw.Keys)
        {
            if (key.StartsWith("exif:GPS", StringComparison.OrdinalIgnoreCase)
                || key.Contains(":gps", StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(key);
            }
        }
        foreach (string key in toRemove)
        {
            raw.Remove(key);
        }
    }

}
