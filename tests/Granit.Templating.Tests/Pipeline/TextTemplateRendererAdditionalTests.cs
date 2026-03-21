using Granit.Templating.GlobalContext;
using Granit.Templating.Internal;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Pipeline;

public sealed class TextTemplateRendererAdditionalTests
{
    private sealed record DocTestData(string Title);

    private sealed class DocTestTemplateType : TextTemplateType<DocTestData>
    {
        public override string Name => "Test.Document";
    }

    private static readonly TextTemplateType<DocTestData> DocType = new DocTestTemplateType();

    private static readonly TemplateDescriptor HtmlDescriptor = new()
    {
        Content = "Hello",
        MimeType = "text/html",
    };

    [Fact]
    public async Task RenderAsync_WhenEngineReturnsBinary_ThrowsInvalidOperationException()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<DocTestData>(),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new BinaryRenderedContent(ReadOnlyMemory<byte>.Empty, DocumentFormat.Pdf));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new([resolver], [engine], [], sp);

        Func<Task> act = async () =>
            await sut.RenderAsync(DocType, new DocTestData("Test"),
                TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("BinaryRenderedContent");
    }

    [Fact]
    public async Task RenderAsync_WhenNoEngineCanRender_ThrowsInvalidOperationException()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(false);

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new([resolver], [engine], [], sp);

        Func<Task> act = async () =>
            await sut.RenderAsync(DocType, new DocTestData("Test"),
                TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("No ITemplateEngine");
    }

    [Fact]
    public async Task RenderDocumentAsync_ReturnsRenderedContent()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        TextRenderedContent expectedContent = new("<h1>doc</h1>", DocumentFormat.Pdf);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<DocTestData>(),
                DocumentFormat.Pdf,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedContent);

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new([resolver], [engine], [], sp);

        RenderedContent result = await sut.RenderDocumentAsync(
            DocType, new DocTestData("Test"), DocumentFormat.Pdf,
            TestContext.Current.CancellationToken);

        result.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task RenderAsync_ResolversByPriority_HighestPriorityWins()
    {
        TemplateDescriptor highPriDesc = new()
        {
            Content = "high-priority",
            MimeType = "text/html",
        };

        ITemplateResolver lowResolver = Substitute.For<ITemplateResolver>();
        lowResolver.Priority.Returns(10);
        lowResolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateResolver highResolver = Substitute.For<ITemplateResolver>();
        highResolver.Priority.Returns(200);
        highResolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(highPriDesc);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<DocTestData>(),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TemplateDescriptor desc = call.Arg<TemplateDescriptor>();
                return new TextRenderedContent(desc.Content, DocumentFormat.Html);
            });

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new([lowResolver, highResolver], [engine], [], sp);

        RenderedTextResult result = await sut.RenderAsync(
            DocType, new DocTestData("Test"),
            TestContext.Current.CancellationToken);

        result.Html.ShouldBe("high-priority");
    }

    [Fact]
    public async Task RenderAsync_GlobalContextsPassedToEngine()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateGlobalContext globalCtx = Substitute.For<ITemplateGlobalContext>();
        globalCtx.ContextName.Returns("test");
        globalCtx.Resolve().Returns(new { value = "ctx-data" });

        IReadOnlyList<ITemplateGlobalContext>? capturedContexts = null;
        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<DocTestData>(),
                DocumentFormat.Html,
                Arg.Do<IReadOnlyList<ITemplateGlobalContext>>(c => capturedContexts = c),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("ok", DocumentFormat.Html));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new([resolver], [engine], [globalCtx], sp);

        await sut.RenderAsync(DocType, new DocTestData("Test"),
            TestContext.Current.CancellationToken);

        capturedContexts.ShouldNotBeNull();
        capturedContexts!.Count.ShouldBe(1);
        capturedContexts[0].ContextName.ShouldBe("test");
    }
}
