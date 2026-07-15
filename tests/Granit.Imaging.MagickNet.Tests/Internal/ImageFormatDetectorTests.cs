using System.Text;
using Granit.Imaging.MagickNet.Internal;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

/// <summary>
/// Direct coverage of the magic-byte allowlist — the security control that keeps dangerous
/// formats (SVG, MSL, MVG, PDF, EPS) away from the native ImageMagick decoder.
/// </summary>
public sealed class ImageFormatDetectorTests
{
    private static byte[] Pad(byte[] header)
    {
        byte[] padded = new byte[ImageFormatDetector.RequiredHeaderLength];
        header.CopyTo(padded, 0);
        return padded;
    }

    public static TheoryData<string, byte[]> SafeHeaders => new()
    {
        { "jpeg", Pad([0xFF, 0xD8, 0xFF, 0xE0]) },
        { "png", Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) },
        { "gif87a", Pad("GIF87a"u8.ToArray()) },
        { "gif89a", Pad("GIF89a"u8.ToArray()) },
        { "bmp", Pad("BM"u8.ToArray()) },
        { "tiff-le", Pad([0x49, 0x49, 0x2A, 0x00]) },
        { "tiff-be", Pad([0x4D, 0x4D, 0x00, 0x2A]) },
        { "webp", [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50] },
        { "avif", [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66] },
        { "avis", [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x73] },
    };

    [Theory]
    [MemberData(nameof(SafeHeaders))]
    public void Accepts_every_allowlisted_raster_format(string format, byte[] header)
    {
        format.ShouldNotBeNullOrEmpty();
        ImageFormatDetector.IsSafeRasterFormat(header).ShouldBeTrue();
    }

    public static TheoryData<string, byte[]> DangerousHeaders => new()
    {
        { "svg", Pad(Encoding.ASCII.GetBytes("<svg xmlns=\"")) },
        { "xml-prolog", Pad(Encoding.ASCII.GetBytes("<?xml versio")) },
        { "pdf", Pad(Encoding.ASCII.GetBytes("%PDF-1.7")) },
        { "eps", Pad(Encoding.ASCII.GetBytes("%!PS-Adobe-3")) },
        { "msl", Pad(Encoding.ASCII.GetBytes("<msl>")) },
        { "mvg", Pad(Encoding.ASCII.GetBytes("push graphic")) },
        { "riff-wave-not-webp", [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45] },
        { "ftyp-mp42-not-avif", [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32] },
        { "all-zero", new byte[12] },
    };

    [Theory]
    [MemberData(nameof(DangerousHeaders))]
    public void Rejects_dangerous_or_unknown_formats(string format, byte[] header)
    {
        format.ShouldNotBeNullOrEmpty();
        ImageFormatDetector.IsSafeRasterFormat(header).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_truncated_header_shorter_than_twelve_bytes()
    {
        // A valid PNG signature cut to 11 bytes must be rejected: the detector needs the
        // full window to vet container formats (WebP, AVIF) safely.
        byte[] truncated = new byte[11];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(truncated, 0);

        ImageFormatDetector.IsSafeRasterFormat(truncated).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_an_empty_span() =>
        ImageFormatDetector.IsSafeRasterFormat([]).ShouldBeFalse();
}
