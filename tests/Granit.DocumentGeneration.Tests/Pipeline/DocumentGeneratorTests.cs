using System.Diagnostics.Metrics;
using Granit.DocumentGeneration.Diagnostics;
using Granit.DocumentGeneration.Exceptions;
using Granit.DocumentGeneration.Internal;
using Granit.DocumentGeneration.Pipeline;
using Granit.MultiTenancy;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Tests.Pipeline;

// Declared at namespace scope so NSubstitute can proxy
internal sealed record InvoiceData(string CustomerName, decimal Amount);

internal sealed class InvoiceTemplateType : DocumentTemplateType<InvoiceData>
{
    public override string Name => "Billing.Invoice";
}

public sealed class DocumentGeneratorTests
{
    private static readonly InvoiceTemplateType TemplateType = new();

    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF magic

    private static readonly DocumentGenerationMetrics Metrics = CreateMetrics();
    private static readonly ICurrentTenant Tenant = CreateTenant();

    private static DocumentGenerationMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new DocumentGenerationMetrics(factory);
    }

    private static ICurrentTenant CreateTenant()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        return tenant;
    }

    [Fact]
    public async Task GenerateAsync_WithMatchingRenderer_ReturnsDocumentResult()
    {
        // Arrange
        InvoiceData data = new("Hôpital Saint-Luc", 1500m);

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<p>Invoice for Hôpital Saint-Luc</p>", DocumentFormat.Pdf));

        IDocumentRenderer pdfRenderer = Substitute.For<IDocumentRenderer>();
        pdfRenderer.CanRender(DocumentFormat.Pdf).Returns(true);
        pdfRenderer.RenderAsync(
                Arg.Any<string>(),
                DocumentFormat.Pdf,
                Arg.Any<CancellationToken>())
            .Returns(new DocumentResult(PdfBytes, DocumentFormat.Pdf));

        DocumentGenerator sut = new(textRenderer, [pdfRenderer], Metrics, Tenant);

        // Act
        DocumentResult result = await sut.GenerateAsync(
            TemplateType, data, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Format.ShouldBe(DocumentFormat.Pdf);
        result.Content.ToArray().ShouldBe(PdfBytes);
    }

    [Fact]
    public async Task GenerateAsync_UsesDefaultFormatFromTemplateType()
    {
        // Arrange — no explicit format override; template default is Pdf
        InvoiceData data = new("Clinique", 200m);

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<p>ok</p>", DocumentFormat.Pdf));

        IDocumentRenderer pdfRenderer = Substitute.For<IDocumentRenderer>();
        pdfRenderer.CanRender(DocumentFormat.Pdf).Returns(true);
        pdfRenderer.RenderAsync(
                Arg.Any<string>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new DocumentResult(PdfBytes, DocumentFormat.Pdf));

        DocumentGenerator sut = new(textRenderer, [pdfRenderer], Metrics, Tenant);

        // Act — no targetFormat parameter
        DocumentResult result = await sut.GenerateAsync(
            TemplateType, data, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — Pdf was passed to the renderer
        await pdfRenderer.Received(1).RenderAsync(
            Arg.Any<string>(),
            DocumentFormat.Pdf,
            Arg.Any<CancellationToken>());

        result.Format.ShouldBe(DocumentFormat.Pdf);
    }

    [Fact]
    public async Task GenerateAsync_WithFormatOverride_UsesOverriddenFormat()
    {
        // Arrange
        InvoiceData data = new("Test", 0m);

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<html/>", DocumentFormat.Excel));

        IDocumentRenderer excelRenderer = Substitute.For<IDocumentRenderer>();
        excelRenderer.CanRender(DocumentFormat.Excel).Returns(true);
        excelRenderer.RenderAsync(
                Arg.Any<string>(),
                DocumentFormat.Excel,
                Arg.Any<CancellationToken>())
            .Returns(new DocumentResult(new byte[] { 0x50, 0x4B }, DocumentFormat.Excel));

        DocumentGenerator sut = new(textRenderer, [excelRenderer], Metrics, Tenant);

        // Act — override default Pdf with Excel
        DocumentResult result = await sut.GenerateAsync(
            TemplateType, data,
            targetFormat: DocumentFormat.Excel,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Format.ShouldBe(DocumentFormat.Excel);
    }

    [Fact]
    public async Task GenerateAsync_NoMatchingRenderer_ThrowsDocumentRendererNotFoundException()
    {
        // Arrange — renderer only handles Excel, but we request Pdf
        InvoiceData data = new("X", 0m);

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<html/>", DocumentFormat.Pdf));

        IDocumentRenderer excelOnly = Substitute.For<IDocumentRenderer>();
        excelOnly.CanRender(DocumentFormat.Pdf).Returns(false);
        excelOnly.CanRender(DocumentFormat.Excel).Returns(true);

        DocumentGenerator sut = new(textRenderer, [excelOnly], Metrics, Tenant);

        // Act
        Func<Task> act = async () =>
            await sut.GenerateAsync(
                TemplateType, data,
                targetFormat: DocumentFormat.Pdf,
                cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        DocumentRendererNotFoundException ex = await Should.ThrowAsync<DocumentRendererNotFoundException>(act);
        ex.Format.ShouldBe(DocumentFormat.Pdf);
    }

    [Fact]
    public async Task GenerateAsync_HtmlPassedToRenderer_IsFromTextPipeline()
    {
        // Arrange — verifies the rendered HTML is forwarded as-is to the document renderer
        InvoiceData data = new("Dupont", 999m);
        const string expectedHtml = "<h1>Invoice</h1><p>Total: 999</p>";
        string? capturedHtml = null;

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent(expectedHtml, DocumentFormat.Pdf));

        IDocumentRenderer renderer = Substitute.For<IDocumentRenderer>();
        renderer.CanRender(DocumentFormat.Pdf).Returns(true);
        renderer.RenderAsync(
                Arg.Do<string>(h => capturedHtml = h),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new DocumentResult(PdfBytes, DocumentFormat.Pdf));

        DocumentGenerator sut = new(textRenderer, [renderer], Metrics, Tenant);

        // Act
        await sut.GenerateAsync(
            TemplateType, data, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        capturedHtml.ShouldBe(expectedHtml);
    }

    [Fact]
    public async Task GenerateAsync_BinaryEngineResult_ReturnedDirectlyWithoutRenderer()
    {
        // Arrange — binary engine (e.g. ClosedXML) returns BinaryRenderedContent directly
        InvoiceData data = new("Dupont", 123m);
        byte[] excelBytes = [0x50, 0x4B, 0x03, 0x04]; // PK (ZIP magic, XLSX)

        ITextTemplateRenderer textRenderer = Substitute.For<ITextTemplateRenderer>();
        textRenderer.RenderDocumentAsync(
                Arg.Any<InvoiceTemplateType>(),
                Arg.Any<InvoiceData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<CancellationToken>())
            .Returns(new BinaryRenderedContent(excelBytes, DocumentFormat.Excel));

        IDocumentRenderer renderer = Substitute.For<IDocumentRenderer>();

        DocumentGenerator sut = new(textRenderer, [renderer], Metrics, Tenant);

        // Act
        DocumentResult result = await sut.GenerateAsync(
            TemplateType, data,
            targetFormat: DocumentFormat.Excel,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert — binary content returned directly, no IDocumentRenderer invoked
        result.Format.ShouldBe(DocumentFormat.Excel);
        result.Content.ToArray().ShouldBe(excelBytes);
        await renderer.DidNotReceive().RenderAsync(
            Arg.Any<string>(),
            Arg.Any<DocumentFormat>(),
            Arg.Any<CancellationToken>());
    }
}
