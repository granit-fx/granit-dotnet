using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Pdf.Internal;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Pdf.Tests;

public sealed class PdfMetadataExtractorTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("application/pdf ", true)]
    [InlineData("image/jpeg", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void CanHandle_matches_only_application_pdf(string mime, bool expected) =>
        new PdfMetadataExtractor().CanHandle(mime).ShouldBe(expected);

    [Fact]
    public async Task Extract_projects_typed_columns_and_dumps_raw_pdf_prefix()
    {
        byte[] pdf = MinimalPdfBuilder.Build(new Dictionary<string, string>
        {
            ["Title"] = "Granit AssetMetadata F17.6",
            ["Author"] = "JF Meyers",
            ["Subject"] = "PDF metadata extractor test",
            ["Keywords"] = "pdf,metadata,granit",
            ["Producer"] = "Granit Test Suite",
            ["Creator"] = "MinimalPdfBuilder",
        });

        await using MemoryStream stream = new(pdf);
        AssetMetadataResult result = await new PdfMetadataExtractor()
            .ExtractAsync(stream, "application/pdf", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("pdf");
        result.PageCount.ShouldBe(1);
        result.Title.ShouldBe("Granit AssetMetadata F17.6");
        result.Author.ShouldBe("JF Meyers");
        result.Subject.ShouldBe("PDF metadata extractor test");
        result.Keywords.ShouldBe("pdf,metadata,granit");
        result.Producer.ShouldBe("Granit Test Suite");

        result.RawMetadata.ShouldContainKey("pdf:Title");
        result.RawMetadata.ShouldContainKey("pdf:Author");
        result.RawMetadata.ShouldContainKey("pdf:Producer");
        result.RawMetadata.ShouldContainKey("pdf:Creator");
        result.RawMetadata["pdf:Title"].ShouldBe("Granit AssetMetadata F17.6");
    }

    [Fact]
    public async Task Extract_pdf_without_info_dictionary_returns_page_count_only()
    {
        byte[] pdf = MinimalPdfBuilder.Build(metadata: null);

        await using MemoryStream stream = new(pdf);
        AssetMetadataResult result = await new PdfMetadataExtractor()
            .ExtractAsync(stream, "application/pdf", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("pdf");
        result.PageCount.ShouldBe(1);
        result.Title.ShouldBeNull();
        result.Author.ShouldBeNull();
    }

    [Fact]
    public async Task Extract_works_with_non_seekable_stream()
    {
        byte[] pdf = MinimalPdfBuilder.Build(new Dictionary<string, string>
        {
            ["Title"] = "Stream Test",
        });

        await using NonSeekableStream stream = new(pdf);
        AssetMetadataResult result = await new PdfMetadataExtractor()
            .ExtractAsync(stream, "application/pdf", TestContext.Current.CancellationToken);

        result.Title.ShouldBe("Stream Test");
        result.PageCount.ShouldBe(1);
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// Builds a minimal, spec-compliant PDF 1.4 document (one blank page) with an
/// optional <c>/Info</c> dictionary. Synthesised at runtime so the repo never
/// carries committed binary fixtures.
/// </summary>
internal static class MinimalPdfBuilder
{
    public static byte[] Build(IReadOnlyDictionary<string, string>? metadata)
    {
        using MemoryStream ms = new();
        using StreamWriter writer = new(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            NewLine = "\n",
        };

        var offsets = new List<long>();

        // PDF header. The four high-bit bytes hint to tools that the file is binary.
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        ms.WriteByte(0x25);
        ms.WriteByte(0xE2);
        ms.WriteByte(0xE3);
        ms.WriteByte(0xCF);
        ms.WriteByte(0xD3);
        ms.WriteByte((byte)'\n');

        // 1: Catalog
        offsets.Add(ms.Position);
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 2: Pages
        offsets.Add(ms.Position);
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // 3: Page
        offsets.Add(ms.Position);
        writer.Write(
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>\nendobj\n");

        // 4: Info (optional)
        long? infoOffset = null;
        if (metadata is { Count: > 0 })
        {
            writer.Flush();
            infoOffset = ms.Position;
            offsets.Add(ms.Position);
            writer.Write("4 0 obj\n<<");
            foreach (KeyValuePair<string, string> pair in metadata)
            {
                writer.Write($" /{pair.Key} ({EscapePdfString(pair.Value)})");
            }
            writer.Write(" >>\nendobj\n");
        }

        // xref
        writer.Flush();
        long xrefOffset = ms.Position;
        int objectCount = offsets.Count + 1; // +1 for object 0
        writer.Write($"xref\n0 {objectCount}\n");
        writer.Write("0000000000 65535 f \n");
        foreach (long off in offsets)
        {
            writer.Write(off.ToString("D10", CultureInfo.InvariantCulture));
            writer.Write(" 00000 n \n");
        }

        // trailer
        writer.Write("trailer\n<< /Size ");
        writer.Write(objectCount.ToString(CultureInfo.InvariantCulture));
        writer.Write(" /Root 1 0 R");
        if (infoOffset is not null)
        {
            writer.Write(" /Info 4 0 R");
        }
        writer.Write(" >>\nstartxref\n");
        writer.Write(xrefOffset.ToString(CultureInfo.InvariantCulture));
        writer.Write("\n%%EOF\n");

        writer.Flush();
        return ms.ToArray();
    }

    private static string EscapePdfString(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                case '(':
                case ')':
                    sb.Append('\\').Append(c);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
