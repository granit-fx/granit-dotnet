using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Internal;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests;

public sealed class ScribanTemplateEngineCachingTests
{
    private sealed record CacheTestData(string Value);

    [Fact]
    public async Task RenderAsync_SameRevisionId_UsesCachedTemplate()
    {
        ScribanTemplateEngine sut = new();
        var revisionId = Guid.NewGuid();

        TemplateDescriptor descriptor = new()
        {
            Content = "{{ model.value }}",
            MimeType = "text/html",
            RevisionId = revisionId,
        };

        RenderedContent result1 = await sut.RenderAsync(
            descriptor, new CacheTestData("first"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        RenderedContent result2 = await sut.RenderAsync(
            descriptor, new CacheTestData("second"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result1.ShouldBeOfType<TextRenderedContent>().Html.ShouldBe("first");
        result2.ShouldBeOfType<TextRenderedContent>().Html.ShouldBe("second");
    }

    [Fact]
    public async Task RenderAsync_NullRevisionId_CachesByContent()
    {
        ScribanTemplateEngine sut = new();

        TemplateDescriptor descriptor = new()
        {
            Content = "Hello {{ model.value }}",
            MimeType = "text/html",
            RevisionId = null,
        };

        RenderedContent result1 = await sut.RenderAsync(
            descriptor, new CacheTestData("World"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        RenderedContent result2 = await sut.RenderAsync(
            descriptor, new CacheTestData("Again"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result1.ShouldBeOfType<TextRenderedContent>().Html.ShouldBe("Hello World");
        result2.ShouldBeOfType<TextRenderedContent>().Html.ShouldBe("Hello Again");
    }

    [Fact]
    public async Task RenderAsync_PropagatesTargetFormat()
    {
        ScribanTemplateEngine sut = new();

        TemplateDescriptor descriptor = new()
        {
            Content = "content",
            MimeType = "text/html",
        };

        RenderedContent result = await sut.RenderAsync(
            descriptor, new CacheTestData("x"), DocumentFormat.Pdf, [],
            TestContext.Current.CancellationToken);

        TextRenderedContent textResult = result.ShouldBeOfType<TextRenderedContent>();
        textResult.TargetFormat.ShouldBe(DocumentFormat.Pdf);
    }

    [Fact]
    public async Task RenderAsync_MultipleGlobalContexts_AllInjected()
    {
        ScribanTemplateEngine sut = new();

        TemplateDescriptor descriptor = new()
        {
            Content = "{{ ctx1.a }}-{{ ctx2.b }}",
            MimeType = "text/html",
        };

        ITemplateGlobalContext ctx1 = new TestGlobalContext("ctx1", new { a = "val1" });
        ITemplateGlobalContext ctx2 = new TestGlobalContext("ctx2", new { b = "val2" });

        RenderedContent result = await sut.RenderAsync(
            descriptor, new CacheTestData("x"), DocumentFormat.Html, [ctx1, ctx2],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>().Html.ShouldBe("val1-val2");
    }

    private sealed class TestGlobalContext(string name, object data) : ITemplateGlobalContext
    {
        public string ContextName => name;
        public object Resolve() => data;
    }
}
