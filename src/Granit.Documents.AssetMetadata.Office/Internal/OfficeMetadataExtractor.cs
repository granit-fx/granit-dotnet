using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Packaging;
using Granit.Documents.AssetMetadata.Extractors;

namespace Granit.Documents.AssetMetadata.Office.Internal;

/// <summary>
/// Office (OOXML) extractor built on the DocumentFormat.OpenXml NuGet (Microsoft,
/// MIT). Reads <c>core.xml</c> (<see cref="OpenXmlPackage.PackageProperties"/>)
/// and <c>app.xml</c> (<see cref="OpenXmlPackage.ExtendedFilePropertiesPart"/>),
/// projects the well-known fields into the typed columns of
/// <see cref="AssetMetadataResult"/>, and preserves every property verbatim in
/// <see cref="AssetMetadataResult.RawMetadata"/> under the <c>office:</c> prefix.
/// </summary>
/// <remarks>
/// Legacy binary formats (.doc / .xls / .ppt) are out of scope — OpenXml does
/// not read them and the available legacy parsers are LGPL.
/// </remarks>
internal sealed class OfficeMetadataExtractor : IAssetMetadataExtractor
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    /// <inheritdoc />
    public string Name => "office";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType)
    {
        if (string.IsNullOrWhiteSpace(sourceContentType))
        {
            return false;
        }

        string normalised = sourceContentType.Trim();
        return string.Equals(normalised, Docx, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalised, Xlsx, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalised, Pptx, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<AssetMetadataResult> ExtractAsync(
        Stream source, string sourceContentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);

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

            string normalised = sourceContentType.Trim();
            using OpenXmlPackage package = OpenPackage(readable, normalised);

            var raw = new Dictionary<string, string?>(StringComparer.Ordinal);

#pragma warning disable OOXML0001 // IPackageProperties is marked experimental; stable for our read-only metadata projection.
            IPackageProperties core = package.PackageProperties;
#pragma warning restore OOXML0001
            ExtendedFilePropertiesPart? appPart = package switch
            {
                WordprocessingDocument w => w.ExtendedFilePropertiesPart,
                SpreadsheetDocument s => s.ExtendedFilePropertiesPart,
                PresentationDocument p => p.ExtendedFilePropertiesPart,
                _ => null,
            };
            string? title = NullIfEmpty(core.Title);
            string? author = NullIfEmpty(core.Creator);
            string? subject = NullIfEmpty(core.Subject);
            string? keywords = NullIfEmpty(core.Keywords);
            string? lastModifiedBy = NullIfEmpty(core.LastModifiedBy);
            int? revision = ParseInt(core.Revision);

            AddRaw(raw, "Title", title);
            AddRaw(raw, "Creator", author);
            AddRaw(raw, "Subject", subject);
            AddRaw(raw, "Keywords", keywords);
            AddRaw(raw, "LastModifiedBy", lastModifiedBy);
            AddRaw(raw, "Revision", NullIfEmpty(core.Revision));
            AddRaw(raw, "Description", NullIfEmpty(core.Description));
            AddRaw(raw, "Category", NullIfEmpty(core.Category));
            AddRaw(raw, "ContentStatus", NullIfEmpty(core.ContentStatus));
            AddRaw(raw, "Identifier", NullIfEmpty(core.Identifier));
            AddRaw(raw, "Language", NullIfEmpty(core.Language));
            AddRaw(raw, "Version", NullIfEmpty(core.Version));
            AddRaw(raw, "Created", FormatDate(core.Created));
            AddRaw(raw, "Modified", FormatDate(core.Modified));

            string? producer = null;
            int? pageCount = null;

            if (appPart?.Properties is Properties app)
            {
                foreach ((string key, string? value) in EnumerateAppProperties(app))
                {
                    AddRaw(raw, key, value);
                }

                producer = NullIfEmpty(app.Application?.Text);

                if (string.Equals(normalised, Docx, StringComparison.OrdinalIgnoreCase))
                {
                    pageCount = ParseInt(app.Pages?.Text);
                }
                else if (string.Equals(normalised, Pptx, StringComparison.OrdinalIgnoreCase))
                {
                    pageCount = ParseInt(app.Slides?.Text);
                }
            }

            // Slide count fallback for pptx — the part may be absent but slides are still on disk.
            if (pageCount is null && package is PresentationDocument presentation)
            {
                int slideParts = presentation.PresentationPart?.SlideParts.Count() ?? 0;
                if (slideParts > 0)
                {
                    pageCount = slideParts;
                }
            }

            var result = new AssetMetadataResult("office", new Dictionary<string, string?>(StringComparer.Ordinal))
            {
                PageCount = pageCount,
                Title = title,
                Author = author,
                Subject = subject,
                Keywords = keywords,
                Producer = producer,
                Revision = revision,
                LastModifiedBy = lastModifiedBy,
            };

            return Task.FromResult(result with { RawMetadata = raw });
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    private static OpenXmlPackage OpenPackage(Stream stream, string contentType)
    {
        if (string.Equals(contentType, Docx, StringComparison.OrdinalIgnoreCase))
        {
            return WordprocessingDocument.Open(stream, false);
        }
        if (string.Equals(contentType, Xlsx, StringComparison.OrdinalIgnoreCase))
        {
            return SpreadsheetDocument.Open(stream, false);
        }
        return PresentationDocument.Open(stream, false);
    }

    private static IEnumerable<(string Key, string? Value)> EnumerateAppProperties(Properties app)
    {
        foreach (DocumentFormat.OpenXml.OpenXmlElement child in app.ChildElements)
        {
            string name = child.LocalName;
            string? value = NullIfEmpty(child.InnerText);
            if (value is not null)
            {
                yield return (name, value);
            }
        }
    }

    private static void AddRaw(Dictionary<string, string?> raw, string key, string? value)
    {
        if (value is not null)
        {
            raw[$"office:{key}"] = value;
        }
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

    private static string? FormatDate(DateTime? value) =>
        value is { } d ? d.ToString("O", CultureInfo.InvariantCulture) : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
