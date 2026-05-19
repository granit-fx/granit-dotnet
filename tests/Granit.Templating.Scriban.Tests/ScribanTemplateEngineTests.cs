using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Exceptions;
using Granit.Templating.Scriban.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests;

// Record at namespace scope so NSubstitute can proxy generic interfaces
internal sealed record PersonModel(string FirstName, string LastName, int? Age = null);

public sealed class ScribanTemplateEngineTests
{
    private static readonly ScribanTemplateEngine Sut = new(new ServiceCollection().BuildServiceProvider());

    // ---- CanRender -------------------------------------------------------

    [Theory]
    [InlineData("text/html")]
    [InlineData("TEXT/HTML")]
    [InlineData("text/plain")]
    public void CanRender_SupportedMimeTypes_ReturnsTrue(string mimeType)
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "hello",
            MimeType = mimeType,
        };

        Sut.CanRender(descriptor).ShouldBeTrue();
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public void CanRender_UnsupportedMimeTypes_ReturnsFalse(string mimeType)
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "hello",
            MimeType = mimeType,
        };

        Sut.CanRender(descriptor).ShouldBeFalse();
    }

    // ---- RenderAsync — happy path ----------------------------------------

    [Fact]
    public async Task RenderAsync_SimpleTemplate_RendersModelProperties()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "Hello {{ model.first_name }} {{ model.last_name }}!",
            MimeType = "text/html",
        };

        PersonModel data = new("Jean", "Dupont");

        RenderedContent result = await Sut.RenderAsync(
            descriptor, data, DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe("Hello Jean Dupont!");
    }

    [Fact]
    public async Task RenderAsync_SnakeCaseRenamer_ConvertsModelProperties()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "{{ model.age }}",
            MimeType = "text/html",
        };

        PersonModel data = new("Alice", "Smith", Age: 30);

        RenderedContent result = await Sut.RenderAsync(
            descriptor, data, DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe("30");
    }

    [Fact]
    public async Task RenderAsync_WithGlobalContext_InjectsContextVariables()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "Culture: {{ ctx.value }}",
            MimeType = "text/html",
        };

        ITemplateGlobalContext globalContext = Substitute.For<ITemplateGlobalContext>();
        globalContext.ContextName.Returns("ctx");
        globalContext.Resolve().Returns(new { value = "fr-BE" });

        RenderedContent result = await Sut.RenderAsync(
            descriptor, new PersonModel("X", "Y"), DocumentFormat.Html,
            [globalContext],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe("Culture: fr-BE");
    }

    [Fact]
    public async Task RenderAsync_PreservesRevisionId()
    {
        var revisionId = Guid.NewGuid();
        TemplateDescriptor descriptor = new()
        {
            Content = "ok",
            MimeType = "text/html",
            RevisionId = revisionId,
        };

        RenderedContent result = await Sut.RenderAsync(
            descriptor, new PersonModel("A", "B"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.RevisionId.ShouldBe(revisionId);
    }

    [Fact]
    public async Task RenderAsync_PlainTextMimeType_ReturnsTextRenderedContent()
    {
        TemplateDescriptor descriptor = new()
        {
            Content = "Hello {{ model.first_name }}",
            MimeType = "text/plain",
        };

        RenderedContent result = await Sut.RenderAsync(
            descriptor, new PersonModel("Bob", "Martin"), DocumentFormat.Html, [],
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<TextRenderedContent>()
            .Html.ShouldBe("Hello Bob");
    }

    // ---- RenderAsync — parse error ---------------------------------------

    [Fact]
    public async Task RenderAsync_InvalidTemplate_ThrowsTemplateParseException()
    {
        // "{{ if }}" is a genuine Scriban parse error: if without a condition expression
        TemplateDescriptor descriptor = new()
        {
            Content = "{{ if }}",
            MimeType = "text/html",
        };

        Func<Task> act = async () =>
            await Sut.RenderAsync(
                descriptor, new PersonModel("X", "Y"), DocumentFormat.Html, [],
                TestContext.Current.CancellationToken);

        TemplateParseException ex = await Should.ThrowAsync<TemplateParseException>(act);
        ex.Errors.Count.ShouldBeGreaterThan(0);
    }
}
