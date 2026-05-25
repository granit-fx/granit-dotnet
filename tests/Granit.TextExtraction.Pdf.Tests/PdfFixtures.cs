using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Granit.TextExtraction.Pdf.Tests;

/// <summary>
/// Builds in-memory PDF fixtures with PdfPig's writer so we don't have to commit binary
/// blobs to git. Each helper returns a fresh byte[] — wrap in a MemoryStream at the
/// call site if the extractor expects a Stream.
/// </summary>
internal static class PdfFixtures
{
    public static byte[] TextOnly(params string[] lines)
    {
        PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        PdfPageBuilder page = builder.AddPage(PageSize.A4);

        double y = 800;
        foreach (string line in lines)
        {
            page.AddText(line, 12, new PdfPoint(50, y), font);
            y -= 20;
        }

        return builder.Build();
    }

    public static byte[] MultiPage(int pageCount, string textPrefix = "Page ")
    {
        PdfDocumentBuilder builder = new();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);

        for (int i = 1; i <= pageCount; i++)
        {
            PdfPageBuilder page = builder.AddPage(PageSize.A4);
            page.AddText($"{textPrefix}{i}", 12, new PdfPoint(50, 800), font);
        }

        return builder.Build();
    }

    public static byte[] Empty()
    {
        PdfDocumentBuilder builder = new();
        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    /// <summary>
    /// Returns a sequence of bytes that begins with the PDF magic but is structurally
    /// broken. PdfPig fails to open it — used to exercise the malformed-PDF branch.
    /// </summary>
    public static byte[] Malformed() => "%PDF-1.4\nnot a pdf"u8.ToArray();
}
