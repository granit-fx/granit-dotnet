using System;
using System.IO;
using ImageMagick;

namespace Granit.Documents.AssetMetadata.Imaging.Internal;

/// <summary>
/// Strips GPS metadata from a JPEG / PNG / WebP image while preserving every other
/// EXIF / IPTC / XMP tag and the embedded ICC colour profile. The scrub operates
/// directly on the EXIF profile's raw TIFF bytes — it rewrites the GPS-IFD pointer
/// entry (TIFF tag <c>0x8825</c>) in IFD0 to an unknown / benign tag id, which
/// causes every TIFF-aware reader to skip the entire GPS sub-IFD. The pixel grid
/// is not decoded; Magick.NET re-emits the original DCT coefficients for JPEG and
/// the original IDAT chunks for PNG.
/// </summary>
/// <remarks>
/// <para>
/// We bypass <see cref="IExifProfile.RemoveValue(ExifTag)"/> because Magick.NET 14.x
/// does not reliably purge sub-IFD entries from the serialised profile bytes
/// (the GPS sub-IFD content is referenced through a pointer, and the cached
/// bytes leak back into the next <c>Write()</c>). Operating on the raw EXIF
/// bytes is the only portable way to guarantee the destination blob carries no
/// GPS payload.
/// </para>
/// <para>
/// We <i>mutate the GPS-IFD pointer entry in place</i> instead of shrinking the
/// IFD — shrinking by 12 bytes would shift every subsequent entry, breaking the
/// absolute value-pointer offsets that EXIF entries (Make / Model / large
/// strings) carry. Mutating in place preserves every other field byte-for-byte.
/// </para>
/// <para>
/// Pure / stateless — safe to invoke from a singleton scope.
/// </para>
/// </remarks>
internal static class JpegGpsScrubber
{
    /// <summary>TIFF tag id of the GPS-IFD pointer in IFD0.</summary>
    private const ushort GpsInfoTag = 0x8825;

    /// <summary>
    /// Returns scrubbed bytes if any GPS metadata was found and stripped, or
    /// <c>null</c> when the source has no GPS payload (the caller should keep the
    /// original blob verbatim — no point re-uploading identical bytes).
    /// </summary>
    public static byte[]? TryScrub(byte[] source, string contentType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (source.Length == 0)
        {
            return null;
        }

        try
        {
            using var image = new MagickImage(source);
            IExifProfile? exif = image.GetExifProfile();
            if (exif is null)
            {
                return null;
            }

            byte[] exifBytes = exif.ToByteArray() ?? [];
            if (exifBytes.Length == 0)
            {
                return null;
            }

            if (!TryStripGpsFromExif(exifBytes, out byte[]? scrubbedExif))
            {
                // No GPS-IFD pointer found — image carries no GPS metadata.
                return null;
            }

            image.RemoveProfile("exif");
            image.SetProfile(new ImageProfile("exif", scrubbedExif!));

            using var output = new MemoryStream();
            image.Write(output);
            return output.ToArray();
        }
        catch (MagickException)
        {
            // Malformed image or unsupported format — caller falls back to the
            // original blob (null return signals "no scrub happened").
            return null;
        }
    }

    /// <summary>
    /// Locates and removes the GPS-IFD pointer entry (tag <see cref="GpsInfoTag"/>)
    /// from IFD0 of a TIFF blob. EXIF blobs from JPEG APP1 segments are TIFF
    /// containers, so the same surgery applies. Returns <c>false</c> when no GPS
    /// entry was found (the caller should treat the source as already-scrubbed).
    /// </summary>
    private static bool TryStripGpsFromExif(byte[] exif, out byte[]? scrubbed)
    {
        scrubbed = null;
        if (exif.Length < 8)
        {
            return false;
        }

        // Magick.NET sometimes prefixes the EXIF profile bytes with the "Exif\0\0"
        // marker that lives inside the JPEG APP1 segment. Skip it if present so
        // the TIFF parser sees a clean byte-order marker.
        int tiffOffset = 0;
        if (exif.Length >= 6
            && exif[0] == 0x45 && exif[1] == 0x78 && exif[2] == 0x69 && exif[3] == 0x66
            && exif[4] == 0x00 && exif[5] == 0x00)
        {
            tiffOffset = 6;
        }
        if (exif.Length < tiffOffset + 8)
        {
            return false;
        }

        // TIFF header: byte-order marker (II / MM) + magic 0x002A + IFD0 offset.
        bool littleEndian;
        if (exif[tiffOffset] == 0x49 && exif[tiffOffset + 1] == 0x49)
        {
            littleEndian = true;
        }
        else if (exif[tiffOffset] == 0x4D && exif[tiffOffset + 1] == 0x4D)
        {
            littleEndian = false;
        }
        else
        {
            return false;
        }

        ushort magic = ReadUInt16(exif, tiffOffset + 2, littleEndian);
        if (magic != 0x002A)
        {
            return false;
        }

        uint ifd0Offset = ReadUInt32(exif, tiffOffset + 4, littleEndian);
        // IFD offsets are relative to the start of the TIFF, not the EXIF blob.
        int ifd0Pos = tiffOffset + (int)ifd0Offset;
        if (ifd0Pos + 2 > exif.Length)
        {
            return false;
        }
        ushort count = ReadUInt16(exif, ifd0Pos, littleEndian);
        // Each IFD entry is 12 bytes; entries start right after the 2-byte count.
        const int EntrySize = 12;
        int entriesStart = ifd0Pos + 2;
        if (entriesStart + count * EntrySize > exif.Length)
        {
            return false;
        }

        int gpsEntryIndex = -1;
        for (int i = 0; i < count; i++)
        {
            int entryPos = entriesStart + i * EntrySize;
            ushort tag = ReadUInt16(exif, entryPos, littleEndian);
            if (tag == GpsInfoTag)
            {
                gpsEntryIndex = i;
                break;
            }
        }
        if (gpsEntryIndex < 0)
        {
            return false;
        }

        // Mutate the GPS-IFD pointer entry in place — DON'T remove it. Shrinking
        // the IFD by 12 bytes would shift the byte offsets of every entry after
        // it, breaking the absolute value-pointer offsets that other entries
        // (Make / Model / large strings) carry. Instead, rewrite the GPS entry's
        // tag id to a benign / unknown value (0xEA1C, undefined in EXIF / TIFF
        // 6.0) and zero out its type / count / value fields so readers don't
        // surface anything. Every other entry stays at the same offset, so
        // their value-pointers still resolve correctly.
        byte[] output = new byte[exif.Length];
        Buffer.BlockCopy(exif, 0, output, 0, exif.Length);

        int gpsEntryPos = entriesStart + gpsEntryIndex * EntrySize;
        // Tag id: 0xEA1C (PrivateUnknown — not surfaced by any reader we ship).
        WriteUInt16(output, gpsEntryPos, 0xEA1C, littleEndian);
        // Type: BYTE (1), Count: 0, Value: 0 → zero-length payload, nothing to read.
        WriteUInt16(output, gpsEntryPos + 2, 1, littleEndian);
        for (int b = 4; b < EntrySize; b++)
        {
            output[gpsEntryPos + b] = 0;
        }

        scrubbed = output;
        return true;
    }

    private static ushort ReadUInt16(byte[] buffer, int offset, bool littleEndian) =>
        littleEndian
            ? (ushort)(buffer[offset] | (buffer[offset + 1] << 8))
            : (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    private static uint ReadUInt32(byte[] buffer, int offset, bool littleEndian) =>
        littleEndian
            ? (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24))
            : (uint)((buffer[offset] << 24)
                | (buffer[offset + 1] << 16)
                | (buffer[offset + 2] << 8)
                | buffer[offset + 3]);

    private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool littleEndian)
    {
        if (littleEndian)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
        else
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }
    }
}
