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
    internal static bool IsSafeRasterFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < RequiredHeaderLength)
        {
            return false;
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return true;
        }

        // GIF87a / GIF89a
        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
            (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
        {
            return true;
        }

        // BMP: 42 4D ("BM")
        if (header[0] == 0x42 && header[1] == 0x4D)
        {
            return true;
        }

        // TIFF little-endian: 49 49 2A 00
        if (header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00)
        {
            return true;
        }

        // TIFF big-endian: 4D 4D 00 2A
        if (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)
        {
            return true;
        }

        // WebP: "RIFF" + 4 bytes size + "WEBP"
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return true;
        }

        // AVIF: ????ftyp[avif|avis] (ISO BMFF ftyp box at offset 4, brand at offset 8)
        if (header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70 &&
            header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 &&
            (header[11] == 0x66 || header[11] == 0x73))
        {
            return true;
        }

        return false;
    }
}
