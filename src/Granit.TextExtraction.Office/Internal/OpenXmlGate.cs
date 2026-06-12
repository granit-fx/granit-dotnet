using System.IO.Compression;
using Granit.TextExtraction.Options;

namespace Granit.TextExtraction.Office.Internal;

/// <summary>
/// Validates an OpenXml package against the framework's zip-bomb / decompression-bomb
/// caps BEFORE handing it to <c>DocumentFormat.OpenXml</c>. Centralises the gate so the
/// three concrete extractors share the same enforcement.
/// </summary>
/// <remarks>
/// <para>
/// Two complementary checks guard the package:
/// </para>
/// <list type="bullet">
///   <item><b>Cumulative size</b> — the sum of declared uncompressed entry lengths is capped
///   by <see cref="GranitTextExtractionOptions.MaxDecompressedBytes"/>.</item>
///   <item><b>Per-entry compression ratio</b> — zip-bomb defence. The central-directory
///   <see cref="ZipArchiveEntry.Length"/> is a hint, not a guarantee: a crafted package can
///   advertise a tiny size while OpenXml's streaming reader pulls much more from the local
///   header on decompression. We reject any entry whose declared length is more than
///   <see cref="MaxCompressionRatio"/>× its compressed length. Legit Office XML lands
///   between 5:1 and 20:1; 200:1 catches classic zip-bombs (e.g. <c>42.zip</c>-style
///   payloads) while leaving margin for unusually redundant content.</item>
/// </list>
/// </remarks>
internal static class OpenXmlGate
{
    /// <summary>
    /// Per-entry compression ratio ceiling. 200:1 catches classic zip-bomb payloads
    /// without false-positiving on legit Office XML (typically 10–20:1).
    /// </summary>
    internal const long MaxCompressionRatio = 200;

    /// <summary>
    /// Floor below which the ratio check is skipped. Tiny compressed entries can
    /// legitimately yield modest absolute expansions; applying the 200× cap there would
    /// false-positive routine OpenXml relationship parts that compress poorly. Above this
    /// floor a 200× ratio is always anomalous.
    /// </summary>
    internal const long RatioCheckMinCompressedBytes = 1024;

    internal enum GateResult
    {
        Ok,
        TooManyEntries,
        TooLargeDecompressed,
        InvalidPackage,
        SuspiciousCompressionRatio,
    }

    public static GateResult Inspect(byte[] package, GranitTextExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            using MemoryStream stream = new(package, writable: false);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count > options.MaxZipEntries)
            {
                return GateResult.TooManyEntries;
            }

            long total = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                long declared = entry.Length;
                long compressed = entry.CompressedLength;

                // Catch declarations whose ratio is implausible for any
                // legitimate compressible payload (XML, embedded media).
                if (compressed >= RatioCheckMinCompressedBytes &&
                    declared > compressed * MaxCompressionRatio)
                {
                    return GateResult.SuspiciousCompressionRatio;
                }

                // Length is the uncompressed size declared in the central directory. It's a
                // hint not a guarantee — but a zip bomb's entry headers DO advertise huge
                // sizes, so summing this column catches the standard attack surface.
                total += declared;
                if (total > options.MaxDecompressedBytes)
                {
                    return GateResult.TooLargeDecompressed;
                }
            }

            return GateResult.Ok;
        }
        catch (InvalidDataException)
        {
            return GateResult.InvalidPackage;
        }
    }
}
