using Granit.Templating.GlobalContext;
using Granit.Templating.Internal;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Pipeline;

public sealed class RenderedContentTransformerTests
{
    private sealed record TransformerTestData(string Value);

    private sealed class TransformerTestTemplateType : TextTemplateType<TransformerTestData>
    {
        public override string Name => "Test.Transformer";
    }

    private static readonly TextTemplateType<TransformerTestData> TemplateType = new TransformerTestTemplateType();

    private static readonly TemplateDescriptor HtmlDescriptor = new()
    {
        Content = "raw html",
        MimeType = "text/html",
    };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static (ITemplateResolver resolver, ITemplateEngine engine) CreateMocks(string output = "<p>Hello</p>")
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TransformerTestData>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent(output, DocumentFormat.Html));

        return (resolver, engine);
    }

    private static TextTemplateRenderer CreateSut(
        ITemplateResolver resolver,
        ITemplateEngine engine,
        params IRenderedContentTransformer[] transformers) =>
        new([resolver], [engine], [], transformers, new ServiceCollection().BuildServiceProvider(),
            Substitute.For<ILogger<TextTemplateRenderer>>());

    [Fact]
    public async Task RenderAsync_NoTransformers_ReturnsOriginalHtml()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("<p>Original</p>");
        TextTemplateRenderer sut = CreateSut(resolver, engine);

        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TransformerTestData("test"), CancellationToken);

        result.Html.ShouldBe("<p>Original</p>");
    }

    [Fact]
    public async Task RenderAsync_SingleTransformer_TransformsHtml()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("<p>Before</p>");
        IRenderedContentTransformer transformer = CreateTransformer(100, _ => "<p>After</p>");
        TextTemplateRenderer sut = CreateSut(resolver, engine, transformer);

        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TransformerTestData("test"), CancellationToken);

        result.Html.ShouldBe("<p>After</p>");
    }

    [Fact]
    public async Task RenderAsync_MultipleTransformers_ExecuteInOrder()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("start");
        IRenderedContentTransformer first = CreateTransformer(100, s => s + " → first");
        IRenderedContentTransformer second = CreateTransformer(200, s => s + " → second");
        TextTemplateRenderer sut = CreateSut(resolver, engine, second, first); // reversed registration

        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TransformerTestData("test"), CancellationToken);

        result.Html.ShouldBe("start → first → second");
    }

    [Fact]
    public async Task RenderAsync_TransformerWithCanTransformFalse_IsSkipped()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("<p>Original</p>");

        IRenderedContentTransformer skipped = Substitute.For<IRenderedContentTransformer>();
        skipped.Order.Returns(100);
        skipped.CanTransform(Arg.Any<DocumentFormat>()).Returns(false);

        TextTemplateRenderer sut = CreateSut(resolver, engine, skipped);

        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TransformerTestData("test"), CancellationToken);

        result.Html.ShouldBe("<p>Original</p>");
        await skipped.DidNotReceive().TransformAsync(
            Arg.Any<string>(), Arg.Any<DocumentFormat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_TransformerOnlyForHtml_SkippedForPdf()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("<p>Original</p>");

        // Reconfigure engine to return Pdf format
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TransformerTestData>(),
                DocumentFormat.Pdf,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<p>Original</p>", DocumentFormat.Pdf));

        IRenderedContentTransformer htmlOnly = Substitute.For<IRenderedContentTransformer>();
        htmlOnly.Order.Returns(100);
        htmlOnly.CanTransform(DocumentFormat.Html).Returns(true);
        htmlOnly.CanTransform(DocumentFormat.Pdf).Returns(false);

        TextTemplateRenderer sut = CreateSut(resolver, engine, htmlOnly);

        RenderedContent result = await sut.RenderDocumentAsync(
            TemplateType, new TransformerTestData("test"), DocumentFormat.Pdf, CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>();
        ((TextRenderedContent)result).Html.ShouldBe("<p>Original</p>");
    }

    [Fact]
    public async Task RenderAsync_BinaryContent_TransformersSkipped()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TransformerTestData>(),
                DocumentFormat.Excel,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new BinaryRenderedContent(ReadOnlyMemory<byte>.Empty, DocumentFormat.Excel));

        IRenderedContentTransformer transformer = Substitute.For<IRenderedContentTransformer>();
        transformer.Order.Returns(100);
        transformer.CanTransform(Arg.Any<DocumentFormat>()).Returns(true);

        TextTemplateRenderer sut = CreateSut(resolver, engine, transformer);

        RenderedContent result = await sut.RenderDocumentAsync(
            TemplateType, new TransformerTestData("test"), DocumentFormat.Excel, CancellationToken);

        result.ShouldBeOfType<BinaryRenderedContent>();
        await transformer.DidNotReceive().TransformAsync(
            Arg.Any<string>(), Arg.Any<DocumentFormat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_TransformerReturnsUnchanged_PreservesOriginalInstance()
    {
        (ITemplateResolver resolver, ITemplateEngine engine) = CreateMocks("<p>Same</p>");
        IRenderedContentTransformer noOp = CreateTransformer(100, s => s); // identity
        TextTemplateRenderer sut = CreateSut(resolver, engine, noOp);

        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TransformerTestData("test"), CancellationToken);

        result.Html.ShouldBe("<p>Same</p>");
    }

    private static IRenderedContentTransformer CreateTransformer(int order, Func<string, string> transform)
    {
        IRenderedContentTransformer mock = Substitute.For<IRenderedContentTransformer>();
        mock.Order.Returns(order);
        mock.CanTransform(Arg.Any<DocumentFormat>()).Returns(true);
        mock.TransformAsync(Arg.Any<string>(), Arg.Any<DocumentFormat>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(transform(call.Arg<string>())));
        return mock;
    }
}
