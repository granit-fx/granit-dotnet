using Granit.DocumentGeneration.Exceptions;
using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Tests.Exceptions;

public sealed class DocumentRendererNotFoundExceptionTests
{
    [Fact]
    public void Constructor_SetsFormat()
    {
        DocumentRendererNotFoundException exception = new(DocumentFormat.Pdf);

        exception.Format.ShouldBe(DocumentFormat.Pdf);
    }

    [Fact]
    public void Constructor_SetsMessage_ContainingFormat()
    {
        DocumentRendererNotFoundException exception = new(DocumentFormat.Excel);

        exception.Message.ShouldContain("Excel");
        exception.Message.ShouldContain("IDocumentRenderer");
    }

    [Fact]
    public void Constructor_SetsMessage_WithHelpfulGuidance()
    {
        DocumentRendererNotFoundException exception = new(DocumentFormat.Pdf);

        exception.Message.ShouldContain("renderer package");
    }

    [Fact]
    public void IsException()
    {
        DocumentRendererNotFoundException exception = new(DocumentFormat.Pdf);

        exception.ShouldBeAssignableTo<Exception>();
    }

    [Theory]
    [InlineData(DocumentFormat.Pdf)]
    [InlineData(DocumentFormat.Excel)]
    [InlineData(DocumentFormat.Html)]
    public void Constructor_AllFormats_SetFormatCorrectly(DocumentFormat format)
    {
        DocumentRendererNotFoundException exception = new(format);

        exception.Format.ShouldBe(format);
        exception.Message.ShouldContain(format.ToString());
    }
}
