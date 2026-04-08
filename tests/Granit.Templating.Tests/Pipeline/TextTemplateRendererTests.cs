using Granit.Templating.Enrichment;
using Granit.Templating.Exceptions;
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

// Records declared at namespace scope so NSubstitute can proxy generic interfaces over them
internal sealed record TextTestData(string Name, string? EnrichedValue = null);

internal sealed class TextTestTemplateType : TextTemplateType<TextTestData>
{
    public override string Name => "Test.WelcomeEmail";
}

public sealed class TextTemplateRendererTests
{
    private static readonly TextTemplateType<TextTestData> TemplateType = new TextTestTemplateType();

    private static readonly TemplateDescriptor HtmlDescriptor = new()
    {
        Content = "Hello {{ model.name }}",
        MimeType = "text/html",
        RevisionId = null,
    };

    [Fact]
    public async Task RenderAsync_WithMatchingResolver_ReturnsHtml()
    {
        // Arrange
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TextTestData>(),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("Hello World", DocumentFormat.Html));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new(
            [resolver], [engine], [], [], sp, Substitute.For<ILogger<TextTemplateRenderer>>());

        // Act
        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TextTestData("World"), TestContext.Current.CancellationToken);

        // Assert
        result.Html.ShouldBe("Hello World");
    }

    [Fact]
    public async Task RenderAsync_WhenNoResolverMatches_ThrowsTemplateNotFoundException()
    {
        // Arrange
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new(
            [resolver], [engine], [], [], sp, Substitute.For<ILogger<TextTemplateRenderer>>());

        // Act
        Func<Task> act = async () =>
            await sut.RenderAsync(
                TemplateType, new TextTestData("World"), TestContext.Current.CancellationToken);

        // Assert
        TemplateNotFoundException ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.TemplateName.ShouldBe(TemplateType.Name);
    }

    [Fact]
    public async Task RenderAsync_EnrichersAreCalledInOrderBeforeRendering()
    {
        // Arrange
        List<int> callOrder = [];

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TextTestData>(),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<h1>ok</h1>", DocumentFormat.Html));

        ITemplateDataEnricher<TextTestData> enricher1 = Substitute.For<ITemplateDataEnricher<TextTestData>>();
        enricher1.Order.Returns(20);
        enricher1.EnrichAsync(Arg.Any<TextTestData>(), Arg.Any<CancellationToken>())
            .Returns(callArgs =>
            {
                callOrder.Add(20);
                return Task.FromResult(callArgs.Arg<TextTestData>());
            });

        ITemplateDataEnricher<TextTestData> enricher2 = Substitute.For<ITemplateDataEnricher<TextTestData>>();
        enricher2.Order.Returns(10);
        enricher2.EnrichAsync(Arg.Any<TextTestData>(), Arg.Any<CancellationToken>())
            .Returns(callArgs =>
            {
                callOrder.Add(10);
                return Task.FromResult(callArgs.Arg<TextTestData>());
            });

        ServiceCollection services = new();
        services.AddTransient<ITemplateDataEnricher<TextTestData>>(_ => enricher1);
        services.AddTransient<ITemplateDataEnricher<TextTestData>>(_ => enricher2);
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new(
            [resolver], [engine], [], [], sp, Substitute.For<ILogger<TextTemplateRenderer>>());

        // Act
        await sut.RenderAsync(
            TemplateType, new TextTestData("Test"), TestContext.Current.CancellationToken);

        // Assert
        callOrder.ShouldBe(new[] { 10, 20 });
        callOrder.Count.ShouldBe(2, "both enrichers must have been called");
    }

    [Fact]
    public async Task RenderAsync_WithNoEnrichers_PassesOriginalDataToEngine()
    {
        // Arrange
        TextTestData data = new("Alice");
        TextTestData? capturedData = null;

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Do<TextTestData>(d => capturedData = d),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<p>ok</p>", DocumentFormat.Html));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new(
            [resolver], [engine], [], [], sp, Substitute.For<ILogger<TextTemplateRenderer>>());

        // Act
        await sut.RenderAsync(TemplateType, data, TestContext.Current.CancellationToken);

        // Assert
        capturedData.ShouldBe(data);
    }

    [Fact]
    public async Task RenderAsync_CultureSpecificKeyTriedBeforeNeutral()
    {
        // Arrange — resolver only responds to the neutral key
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(
                Arg.Is<TemplateKey>(k => k.Culture != null),
                Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);
        resolver.TryResolveAsync(
                Arg.Is<TemplateKey>(k => k.Culture == null),
                Arg.Any<CancellationToken>())
            .Returns(HtmlDescriptor);

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<TextTestData>(),
                DocumentFormat.Html,
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TextRenderedContent("<p>neutral</p>", DocumentFormat.Html));

        ServiceCollection services = new();
        IServiceProvider sp = services.BuildServiceProvider();

        TextTemplateRenderer sut = new(
            [resolver], [engine], [], [], sp, Substitute.For<ILogger<TextTemplateRenderer>>());

        // Act
        RenderedTextResult result = await sut.RenderAsync(
            TemplateType, new TextTestData("X"), TestContext.Current.CancellationToken);

        // Assert
        result.Html.ShouldBe("<p>neutral</p>");
    }
}
