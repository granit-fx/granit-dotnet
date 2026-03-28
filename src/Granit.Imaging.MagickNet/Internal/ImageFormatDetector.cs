namespace Granit.Imaging.MagickNet.Internal;

/// <summary>
/// Validates image magic bytes against a safe raster format allowlist.
/// Prevents dangerous formats (SVG, MSL, MVG, PDF, EPS) from reaching
/// the native ImageMagick decoder where they could trigger SSRF or file I/O.
/// </summary>
internal static class ImageFormatDetector
{
    internal const int RequiredHeaderLength = 12;

    /// <summary>
    /// Returns <c>true</c> if the header bytes match a known safe raster image format
    /// (JPEG, PNG, GIF, BMP, TIFF, WebP, or AVIF).
    /// </summary>
    internal static bool IsSafeRasterFormat(ReadOnlySpan<byte> header) =>
        header.Length >= RequiredHeaderLength &&
        (IsJpeg(header) || IsPng(header) || IsGif(header) || IsBmp(header) ||
         IsTiffLittleEndian(header) || IsTiffBigEndian(header) ||
         IsWebP(header) || IsAvif(header));

    // JPEG: FF D8 FF
    private static bool IsJpeg(ReadOnlySpan<byte> h) =>
        h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;

    // PNG: 89 50 4E 47
    private static bool IsPng(ReadOnlySpan<byte> h) =>
        h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;

    // GIF87a / GIF89a
    private static bool IsGif(ReadOnlySpan<byte> h) =>
        h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38 &&
        (h[4] == 0x37 || h[4] == 0x39) && h[5] == 0x61;

    // BMP: 42 4D ("BM")
    private static bool IsBmp(ReadOnlySpan<byte> h) =>
        h[0] == 0x42 && h[1] == 0x4D;

    // TIFF little-endian: 49 49 2A 00
    private static bool IsTiffLittleEndian(ReadOnlySpan<byte> h) =>
        h[0] == 0x49 && h[1] == 0x49 && h[2] == 0x2A && h[3] == 0x00;

    // TIFF big-endian: 4D 4D 00 2A
    private static bool IsTiffBigEndian(ReadOnlySpan<byte> h) =>
        h[0] == 0x4D && h[1] == 0x4D && h[2] == 0x00 && h[3] == 0x2A;

    // WebP: "RIFF" + 4 bytes size + "WEBP"
    private static bool IsWebP(ReadOnlySpan<byte> h) =>
        h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 &&
        h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50;

    // AVIF: ????ftyp[avif|avis] (ISO BMFF ftyp box at offset 4, brand at offset 8)
    private static bool IsAvif(ReadOnlySpan<byte> h) =>
        h[4] == 0x66 && h[5] == 0x74 && h[6] == 0x79 && h[7] == 0x70 &&
        h[8] == 0x61 && h[9] == 0x76 && h[10] == 0x69 &&
        (h[11] == 0x66 || h[11] == 0x73);
}
