using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Granit.TextExtraction.Pdf.Ocr.Tests;

/// <summary>
/// In-memory PDF fixtures built with PdfPig's writer — keeps the test suite free of
/// binary blobs and lets us tune page mixtures per scenario.
/// </summary>
internal static class PdfFixtures
{
    /// <summary>
    /// Builds a single-page PDF with no text content. PdfPig returns an empty string
    /// for the page, so the extractor's "scanned page" branch kicks in.
    /// </summary>
    public static byte[] EmptyPage()
    {
        PdfDocumentBuilder builder = new();
        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    /// <summary>
    /// Builds a single-page PDF whose page is filled with enough native text to clear
    /// the default MinNativeCharsPerPage threshold.
    /// </summary>
    public static byte[] TextPage(string text)
    {
        PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        PdfPageBuilder page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(50, 800), font);
        return builder.Build();
    }

    /// <summary>
    /// Builds a multi-page PDF where the first page has native text and the second is
    /// empty (so the OCR path fires only for page 2). Output:
    /// page 1 → <paramref name="firstPageText"/>
    /// page 2 → blank, triggers OCR
    /// </summary>
    public static byte[] MixedTextThenScanned(string firstPageText)
    {
        PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);

        PdfPageBuilder page1 = builder.AddPage(PageSize.A4);
        page1.AddText(firstPageText, 12, new PdfPoint(50, 800), font);

        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    /// <summary>
    /// Builds a PDF with <paramref name="pageCount"/> empty pages — exercises the
    /// MaxPagesToRasterise clamp.
    /// </summary>
    public static byte[] EmptyPages(int pageCount)
    {
        PdfDocumentBuilder builder = new();
        for (int i = 0; i < pageCount; i++)
        {
            builder.AddPage(PageSize.A4);
        }
        return builder.Build();
    }

    /// <summary>Bytes that look like a PDF but aren't — exercises the malformed-PDF branch.</summary>
    public static byte[] Malformed() => "%PDF-1.4\nnot a pdf"u8.ToArray();
}
