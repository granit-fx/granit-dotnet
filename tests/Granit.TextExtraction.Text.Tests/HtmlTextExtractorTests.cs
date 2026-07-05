using System.Text;
using Granit.Html;
using Granit.Html.AngleSharp;
using Granit.TextExtraction.Exceptions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Text.Tests;

public sealed class HtmlTextExtractorTests
{
    private static readonly IHtmlToPlainTextConverter UntrustedConverter =
        new AngleSharpHtmlToPlainTextConverter(AngleSharpConfiguration.BuildForUntrustedContent());

    private static HtmlTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(UntrustedConverter, MEOptions.Create(options ?? new ExtractionOptions()));

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("text/html", true)]
    [InlineData("application/xhtml+xml", true)]
    [InlineData("text/plain", false)]
    [InlineData("text/markdown", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_html_family(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Extracts_plain_text_from_paragraph()
    {
        HtmlTextExtractor extractor = CreateExtractor();
        await using MemoryStream stream = Utf8("<html><body><p>Hello world</p></body></html>");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/html", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("Hello world");
        result.ExtractorName.ShouldBe(HtmlTextExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();
        result.CharCount.ShouldBe(11);
    }

    [Fact]
    public async Task Strips_script_and_style_tags()
    {
        HtmlTextExtractor extractor = CreateExtractor();
        await using MemoryStream stream = Utf8(
            "<html><head><style>body{color:red}</style></head>" +
            "<body><script>alert('x')</script><p>Visible</p></body></html>");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/html", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("Visible");
        result.Content.ShouldNotContain("alert");
        result.Content.ShouldNotContain("color");
    }

    [Fact]
    public async Task Truncates_to_max_char_length_and_flags()
    {
        HtmlTextExtractor extractor = CreateExtractor();
        string body = new('x', 5_000);
        await using MemoryStream stream = Utf8($"<p>{body}</p>");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/html", maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
        result.CharCount.ShouldBe(100);
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions options = new() { MaxBodySizeBytes = 16 };
        HtmlTextExtractor extractor = CreateExtractor(options);
        await using MemoryStream stream = Utf8("<p>" + new string('a', 4096) + "</p>");

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await extractor.ExtractAsync(
                stream, "text/html", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public async Task Heading_followed_by_paragraph_renders_readable_text()
    {
        HtmlTextExtractor extractor = CreateExtractor();
        await using MemoryStream stream = Utf8("<h1>Welcome</h1><p>Body</p>");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/html", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("WELCOME");
        result.Content.ShouldContain("Body");
    }
}
