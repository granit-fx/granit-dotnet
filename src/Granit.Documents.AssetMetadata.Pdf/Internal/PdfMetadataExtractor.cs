using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Extractors;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace Granit.Documents.AssetMetadata.Pdf.Internal;

/// <summary>
/// PDF extractor built on the PdfPig NuGet (Eliot Jones, Apache-2.0). Reads the
/// PDF <c>Information</c> dictionary and projects the well-known fields into the
/// typed columns of <see cref="AssetMetadataResult"/>. Every entry in
/// <c>Information</c> plus the catalog metadata is preserved verbatim in
/// <see cref="AssetMetadataResult.RawMetadata"/> under the <c>pdf:</c> prefix.
/// </summary>
internal sealed class PdfMetadataExtractor : IAssetMetadataExtractor
{
    /// <inheritdoc />
    public string Name => "pdf";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType) =>
        !string.IsNullOrWhiteSpace(sourceContentType)
        && string.Equals(sourceContentType.Trim(), "application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<AssetMetadataResult> ExtractAsync(
        Stream source, string sourceContentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Stream readable = source;
        MemoryStream? buffered = null;
        try
        {
            if (!readable.CanSeek)
            {
                buffered = new MemoryStream();
                readable.CopyTo(buffered);
                buffered.Position = 0;
                readable = buffered;
            }
            else
            {
                readable.Position = 0;
            }

            using var document = PdfDocument.Open(readable);
            var raw = new Dictionary<string, string?>(StringComparer.Ordinal);

            DocumentInformation info = document.Information;
            string? title = NullIfEmpty(info.Title);
            string? author = NullIfEmpty(info.Author);
            string? subject = NullIfEmpty(info.Subject);
            string? keywords = NullIfEmpty(info.Keywords);
            string? producer = NullIfEmpty(info.Producer);
            string? creator = NullIfEmpty(info.Creator);

            AddRaw(raw, "Title", title);
            AddRaw(raw, "Author", author);
            AddRaw(raw, "Subject", subject);
            AddRaw(raw, "Keywords", keywords);
            AddRaw(raw, "Producer", producer);
            AddRaw(raw, "Creator", creator);
            AddRaw(raw, "CreationDate", NullIfEmpty(info.CreationDate));
            AddRaw(raw, "ModifiedDate", NullIfEmpty(info.ModifiedDate));

            // Dump every entry in the document information dictionary verbatim
            // — covers custom keys (e.g. "AAPL:Keywords") not surfaced by the
            // strongly-typed properties.
            if (info.DocumentInformationDictionary is { } dict)
            {
                foreach (KeyValuePair<string, IToken> pair in dict.Data)
                {
                    raw[$"pdf:{pair.Key}"] = TokenToString(pair.Value);
                }
            }

            var result = new AssetMetadataResult("pdf", new Dictionary<string, string?>(StringComparer.Ordinal))
            {
                PageCount = document.NumberOfPages,
                Title = title,
                Author = author,
                Subject = subject,
                Keywords = keywords,
                Producer = producer,
            };

            return Task.FromResult(result with { RawMetadata = raw });
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    private static void AddRaw(Dictionary<string, string?> raw, string key, string? value)
    {
        if (value is not null)
        {
            raw[$"pdf:{key}"] = value;
        }
    }

    private static string? TokenToString(IToken token) => token switch
    {
        StringToken s => s.Data,
        HexToken h => h.Data,
        NameToken n => n.Data,
        NumericToken num => num.Data.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BooleanToken b => b.Data ? "true" : "false",
        _ => token?.ToString(),
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
