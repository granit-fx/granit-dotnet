using System.IO.Compression;
using Granit.TextExtraction.Options;

namespace Granit.TextExtraction.Office.Internal;

/// <summary>
/// Validates an OpenXml package against the framework's zip-bomb / decompression-bomb
/// caps BEFORE handing it to <c>DocumentFormat.OpenXml</c>. Centralises the gate so the
/// three concrete extractors share the same enforcement.
/// </summary>
/// <remarks>
/// We rely on <see cref="ZipArchive"/> to walk the entries without decompressing them — the
/// per-entry <see cref="ZipArchiveEntry.Length"/> is the declared uncompressed size, which is
/// what a zip-bomb inflates. The cumulative check catches both "many small entries"
/// (<see cref="GranitTextExtractionOptions.MaxZipEntries"/>) and "few enormous entries"
/// (<see cref="GranitTextExtractionOptions.MaxDecompressedBytes"/>).
/// </remarks>
internal static class OpenXmlGate
{
    public enum GateResult
    {
        Ok,
        TooManyEntries,
        TooLargeDecompressed,
        InvalidPackage,
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
                // Length is the uncompressed size declared in the central directory. It's a
                // hint not a guarantee — but a zip bomb's entry headers DO advertise huge
                // sizes, so summing this column catches the standard attack surface.
                total += entry.Length;
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
