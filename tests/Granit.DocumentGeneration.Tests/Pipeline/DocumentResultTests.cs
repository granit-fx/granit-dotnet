using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Tests.Pipeline;

public sealed class DocumentResultTests
{
    [Fact]
    public void Constructor_WithoutFileName_DefaultsToNull()
    {
        DocumentResult result = new(ReadOnlyMemory<byte>.Empty, DocumentFormat.Html);

        result.FileName.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        byte[] content = [0x01, 0x02];
        DocumentResult result1 = new(content, DocumentFormat.Pdf, "test.pdf");
        DocumentResult result2 = new(content, DocumentFormat.Pdf, "test.pdf");

        result1.ShouldBe(result2);
    }

    [Fact]
    public void Equality_DifferentFormat_AreNotEqual()
    {
        byte[] content = [0x01, 0x02];
        DocumentResult result1 = new(content, DocumentFormat.Pdf);
        DocumentResult result2 = new(content, DocumentFormat.Excel);

        result1.ShouldNotBe(result2);
    }

    [Fact]
    public void Equality_DifferentFileName_AreNotEqual()
    {
        byte[] content = [0x01, 0x02];
        DocumentResult result1 = new(content, DocumentFormat.Pdf, "a.pdf");
        DocumentResult result2 = new(content, DocumentFormat.Pdf, "b.pdf");

        result1.ShouldNotBe(result2);
    }
}
