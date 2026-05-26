using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Granit.TextExtraction.Office.Tests;

/// <summary>
/// Builds in-memory Office documents (.docx / .xlsx / .pptx) and adversarial archives
/// for testing the extractors' security gates. No committed binary fixtures.
/// </summary>
internal static class OfficeFixtures
{
    public static byte[] Docx(params string[] paragraphs)
    {
        using MemoryStream ms = new();
        using (var doc = WordprocessingDocument.Create(
            ms, WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            W.Document document = new();
            W.Body body = new();

            foreach (string p in paragraphs)
            {
                W.Paragraph paragraph = new();
                W.Run run = new();
                W.Text text = new(p) { Space = SpaceProcessingModeValues.Preserve };
                run.AppendChild(text);
                paragraph.AppendChild(run);
                body.AppendChild(paragraph);
            }

            document.AppendChild(body);
            main.Document = document;
        }

        return ms.ToArray();
    }

    public static byte[] Xlsx(params (string sheet, string[,] rows)[] sheets)
    {
        using MemoryStream ms = new();
        using (var doc = SpreadsheetDocument.Create(
            ms, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook();
            S.Sheets sheetsElem = workbookPart.Workbook.AppendChild(new S.Sheets());

            uint sheetId = 1;
            foreach ((string sheetName, string[,] cells) in sheets)
            {
                WorksheetPart wsPart = workbookPart.AddNewPart<WorksheetPart>();
                S.SheetData sheetData = new();

                int rowCount = cells.GetLength(0);
                int colCount = cells.GetLength(1);
                for (int r = 0; r < rowCount; r++)
                {
                    S.Row row = new();
                    for (int c = 0; c < colCount; c++)
                    {
                        S.Cell cell = new()
                        {
                            DataType = S.CellValues.String,
                            CellValue = new S.CellValue(cells[r, c] ?? string.Empty),
                        };
                        row.AppendChild(cell);
                    }
                    sheetData.AppendChild(row);
                }

                wsPart.Worksheet = new S.Worksheet(sheetData);

                S.Sheet sheet = new()
                {
                    Id = workbookPart.GetIdOfPart(wsPart),
                    SheetId = sheetId++,
                    Name = sheetName,
                };
                sheetsElem.AppendChild(sheet);
            }
        }

        return ms.ToArray();
    }

    public static byte[] Pptx(params string[] slideTexts)
    {
        using MemoryStream ms = new();
        using (var doc = PresentationDocument.Create(
            ms, PresentationDocumentType.Presentation))
        {
            PresentationPart presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            P.SlideIdList slideIdList = new();
            uint slideId = 256;

            foreach (string slideText in slideTexts)
            {
                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(
                        new P.ShapeTree(
                            new P.NonVisualGroupShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                                new P.NonVisualGroupShapeDrawingProperties(),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.GroupShapeProperties(),
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new P.NonVisualDrawingProperties { Id = 2U, Name = "Body" },
                                    new P.NonVisualShapeDrawingProperties(),
                                    new P.ApplicationNonVisualDrawingProperties()),
                                new P.ShapeProperties(),
                                new P.TextBody(
                                    new D.BodyProperties(),
                                    new D.ListStyle(),
                                    new D.Paragraph(
                                        new D.Run(
                                            new D.RunProperties { Language = "en-US" },
                                            new D.Text(slideText))))))));

                P.SlideId id = new() { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) };
                slideIdList.AppendChild(id);
            }

            presentationPart.Presentation.SlideIdList = slideIdList;
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Builds an .xlsx-shaped zip with many trivial entries — exercises the MaxZipEntries gate.
    /// The package is structurally invalid as OpenXml but the gate runs before parsing.
    /// </summary>
    public static byte[] ZipWithEntryCount(int entries)
    {
        using MemoryStream ms = new();
        using (ZipArchive archive = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < entries; i++)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"file-{i}.bin");
                using Stream s = entry.Open();
                s.WriteByte(0);
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a zip with one entry that ADVERTISES a huge uncompressed length — exercises
    /// the MaxDecompressedBytes gate without actually writing the bytes. Real zip-bomb
    /// payloads exploit exactly this column from the central directory.
    /// </summary>
    public static byte[] ZipWithAdvertisedSize(long advertisedBytes)
    {
        using MemoryStream ms = new();
        using (ZipArchive archive = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("payload.bin");
            using Stream s = entry.Open();
            byte[] chunk = new byte[8 * 1024];
            long remaining = advertisedBytes;
            while (remaining > 0)
            {
                int write = (int)Math.Min(chunk.Length, remaining);
                s.Write(chunk, 0, write);
                remaining -= write;
            }
        }
        return ms.ToArray();
    }

    public static byte[] InvalidZip() => Encoding.UTF8.GetBytes("not a zip at all");

    /// <summary>
    /// Builds a single-entry zip whose payload is <paramref name="rawSize"/> bytes of zeros
    /// using <see cref="CompressionLevel.SmallestSize"/>. Zeros compress to a handful of
    /// dictionary tokens, so the resulting CompressedLength/Length ratio comfortably exceeds
    /// the OpenXmlGate's <c>MaxCompressionRatio</c> (200×). Exercises the suspicious
    /// compression-ratio gate branch.
    /// </summary>
    public static byte[] ZipWithHighCompressionRatio(int rawSize)
    {
        using MemoryStream ms = new();
        using (ZipArchive archive = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("zeros.bin", CompressionLevel.SmallestSize);
            using Stream s = entry.Open();
            byte[] chunk = new byte[64 * 1024];
            int remaining = rawSize;
            while (remaining > 0)
            {
                int write = Math.Min(chunk.Length, remaining);
                s.Write(chunk, 0, write);
                remaining -= write;
            }
        }
        return ms.ToArray();
    }
}
