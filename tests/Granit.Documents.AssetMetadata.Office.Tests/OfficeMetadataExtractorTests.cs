using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Granit.Documents.AssetMetadata.Office.Internal;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Office.Tests;

public sealed class OfficeMetadataExtractorTests
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    [Theory]
    [InlineData(Docx, true)]
    [InlineData(Xlsx, true)]
    [InlineData(Pptx, true)]
    [InlineData("APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.WORDPROCESSINGML.DOCUMENT", true)]
    [InlineData("application/msword", false)]
    [InlineData("application/vnd.ms-excel", false)]
    [InlineData("application/vnd.ms-powerpoint", false)]
    [InlineData("application/pdf", false)]
    [InlineData("image/jpeg", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void CanHandle_matches_only_ooxml_mimes(string mime, bool expected) =>
        new OfficeMetadataExtractor().CanHandle(mime).ShouldBe(expected);

    [Fact]
    public async Task Extract_docx_projects_typed_columns_and_dumps_office_prefix()
    {
        byte[] docx = MinimalOfficeBuilder.BuildDocx(new MinimalOfficeBuilder.Metadata
        {
            Title = "Granit AssetMetadata F17.7",
            Author = "JF Meyers",
            Subject = "Office metadata extractor test",
            Keywords = "office,docx,metadata",
            LastModifiedBy = "JF Meyers",
            Revision = "3",
            Pages = 7,
            Application = "Granit Test Suite",
        });

        await using MemoryStream stream = new(docx);
        AssetMetadataResult result = await new OfficeMetadataExtractor()
            .ExtractAsync(stream, Docx, TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("office");
        result.Title.ShouldBe("Granit AssetMetadata F17.7");
        result.Author.ShouldBe("JF Meyers");
        result.Subject.ShouldBe("Office metadata extractor test");
        result.Keywords.ShouldBe("office,docx,metadata");
        result.LastModifiedBy.ShouldBe("JF Meyers");
        result.Revision.ShouldBe(3);
        result.PageCount.ShouldBe(7);
        result.Producer.ShouldBe("Granit Test Suite");

        result.RawMetadata.ShouldContainKey("office:Title");
        result.RawMetadata.ShouldContainKey("office:Creator");
        result.RawMetadata.ShouldContainKey("office:Revision");
        result.RawMetadata.ShouldContainKey("office:Pages");
        result.RawMetadata.ShouldContainKey("office:Application");
        result.RawMetadata["office:Pages"].ShouldBe("7");
    }

    [Fact]
    public async Task Extract_xlsx_has_null_page_count_and_returns_core_props()
    {
        byte[] xlsx = MinimalOfficeBuilder.BuildXlsx(new MinimalOfficeBuilder.Metadata
        {
            Title = "Sheet Title",
            Author = "Excel Author",
            Subject = "Excel subject",
            Keywords = "xlsx",
            LastModifiedBy = "Modifier",
            Revision = "1",
            Application = "Granit Excel",
        });

        await using MemoryStream stream = new(xlsx);
        AssetMetadataResult result = await new OfficeMetadataExtractor()
            .ExtractAsync(stream, Xlsx, TestContext.Current.CancellationToken);

        result.PageCount.ShouldBeNull();
        result.Title.ShouldBe("Sheet Title");
        result.Author.ShouldBe("Excel Author");
        result.Producer.ShouldBe("Granit Excel");
        result.Revision.ShouldBe(1);
        result.RawMetadata.ShouldContainKey("office:Application");
        result.RawMetadata.ShouldNotContainKey("office:Pages");
    }

    [Fact]
    public async Task Extract_pptx_uses_slide_count_for_page_count()
    {
        byte[] pptx = MinimalOfficeBuilder.BuildPptx(new MinimalOfficeBuilder.Metadata
        {
            Title = "Deck",
            Author = "Presenter",
            Subject = "PPTX subject",
            Keywords = "pptx,slides",
            LastModifiedBy = "Presenter",
            Revision = "5",
            Slides = 4,
            Application = "Granit PowerPoint",
        });

        await using MemoryStream stream = new(pptx);
        AssetMetadataResult result = await new OfficeMetadataExtractor()
            .ExtractAsync(stream, Pptx, TestContext.Current.CancellationToken);

        result.Title.ShouldBe("Deck");
        result.Author.ShouldBe("Presenter");
        result.Revision.ShouldBe(5);
        result.PageCount.ShouldBe(4);
        result.RawMetadata.ShouldContainKey("office:Slides");
        result.RawMetadata["office:Slides"].ShouldBe("4");
    }

    [Fact]
    public async Task Extract_works_with_non_seekable_stream()
    {
        byte[] docx = MinimalOfficeBuilder.BuildDocx(new MinimalOfficeBuilder.Metadata
        {
            Title = "Stream Test",
            Pages = 1,
        });

        await using NonSeekableStream stream = new(docx);
        AssetMetadataResult result = await new OfficeMetadataExtractor()
            .ExtractAsync(stream, Docx, TestContext.Current.CancellationToken);

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
/// Builds minimal OOXML documents at runtime with core + app properties set so
/// the repo never carries committed binary fixtures.
/// </summary>
internal static class MinimalOfficeBuilder
{
    public sealed class Metadata
    {
        public string? Title { get; init; }
        public string? Author { get; init; }
        public string? Subject { get; init; }
        public string? Keywords { get; init; }
        public string? LastModifiedBy { get; init; }
        public string? Revision { get; init; }
        public int? Pages { get; init; }
        public int? Slides { get; init; }
        public string? Application { get; init; }
    }

    public static byte[] BuildDocx(Metadata meta)
    {
        using MemoryStream ms = new();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph()));
            ApplyCore(doc.PackageProperties, meta);
            ApplyApp(doc.AddExtendedFilePropertiesPart(), meta);
        }
        return ms.ToArray();
    }

    public static byte[] BuildXlsx(Metadata meta)
    {
        using MemoryStream ms = new();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true))
        {
            WorkbookPart wb = doc.AddWorkbookPart();
            wb.Workbook = new Workbook(new Sheets(new Sheet { Name = "Sheet1", SheetId = 1U, Id = "rId1" }));
            WorksheetPart ws = wb.AddNewPart<WorksheetPart>("rId1");
            ws.Worksheet = new Worksheet(new SheetData());
            ApplyCore(doc.PackageProperties, meta);
            ApplyApp(doc.AddExtendedFilePropertiesPart(), meta);
        }
        return ms.ToArray();
    }

    public static byte[] BuildPptx(Metadata meta)
    {
        // Minimal presentation: empty PresentationPart with only app.xml carrying
        // the slide count. The extractor reads <Slides> from app.xml first; the
        // slide-parts fallback is irrelevant when app.xml is present.
        using MemoryStream ms = new();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, true))
        {
            PresentationPart pres = doc.AddPresentationPart();
            pres.Presentation = new Presentation();
            ApplyCore(doc.PackageProperties, meta);
            ApplyApp(doc.AddExtendedFilePropertiesPart(), meta);
        }
        return ms.ToArray();
    }

#pragma warning disable OOXML0001 // IPackageProperties experimental — used in tests for synthesis.
    private static void ApplyCore(DocumentFormat.OpenXml.Packaging.IPackageProperties core, Metadata meta)
    {
        if (meta.Title is not null)
        {
            core.Title = meta.Title;
        }
        if (meta.Author is not null)
        {
            core.Creator = meta.Author;
        }
        if (meta.Subject is not null)
        {
            core.Subject = meta.Subject;
        }
        if (meta.Keywords is not null)
        {
            core.Keywords = meta.Keywords;
        }
        if (meta.LastModifiedBy is not null)
        {
            core.LastModifiedBy = meta.LastModifiedBy;
        }
        if (meta.Revision is not null)
        {
            core.Revision = meta.Revision;
        }
    }
#pragma warning restore OOXML0001

    private static void ApplyApp(ExtendedFilePropertiesPart part, Metadata meta)
    {
        DocumentFormat.OpenXml.ExtendedProperties.Properties props = new();
        if (meta.Application is not null)
        {
            props.Application = new DocumentFormat.OpenXml.ExtendedProperties.Application { Text = meta.Application };
        }
        if (meta.Pages is not null)
        {
            props.Pages = new DocumentFormat.OpenXml.ExtendedProperties.Pages
            {
                Text = meta.Pages.Value.ToString(CultureInfo.InvariantCulture),
            };
        }
        if (meta.Slides is not null)
        {
            props.Slides = new DocumentFormat.OpenXml.ExtendedProperties.Slides
            {
                Text = meta.Slides.Value.ToString(CultureInfo.InvariantCulture),
            };
        }
        part.Properties = props;
    }
}
