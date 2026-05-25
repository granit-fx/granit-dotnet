using System.Text;
using Granit.TextExtraction.Exceptions;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Text.Tests;

public sealed class MarkdownTextExtractorTests
{
    private static MarkdownTextExtractor CreateExtractor(ExtractionOptions? options = null) =>
        new(MEOptions.Create(options ?? new ExtractionOptions()));

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("text/markdown", true)]
    [InlineData("text/x-markdown", true)]
    [InlineData("text/html", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_markdown_family(string contentType, bool expected) =>
        CreateExtractor().CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Strips_inline_formatting()
    {
        MarkdownTextExtractor extractor = CreateExtractor();
        using MemoryStream stream = Utf8("This is **bold** and _italic_ text.");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/markdown", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("bold");
        result.Content.ShouldContain("italic");
        result.Content.ShouldNotContain("**");
        result.Content.ShouldNotContain("_");
        result.ExtractorName.ShouldBe(MarkdownTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Strips_link_syntax()
    {
        MarkdownTextExtractor extractor = CreateExtractor();
        using MemoryStream stream = Utf8("See [the docs](https://example.com) for details.");

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/markdown", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("the docs");
        result.Content.ShouldContain("details");
        result.Content.ShouldNotContain("[");
        result.Content.ShouldNotContain("](");
    }

    [Fact]
    public async Task Renders_table_text_via_advanced_extensions()
    {
        MarkdownTextExtractor extractor = CreateExtractor();
        const string md = """
            | Col A | Col B |
            | ----- | ----- |
            | Alpha | Beta  |
            """;
        using MemoryStream stream = Utf8(md);

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/markdown", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldContain("Col A");
        result.Content.ShouldContain("Alpha");
        result.Content.ShouldContain("Beta");
        result.Content.ShouldNotContain("|---|");
    }

    [Fact]
    public async Task Truncates_to_max_char_length_and_flags()
    {
        MarkdownTextExtractor extractor = CreateExtractor();
        string body = new('x', 5_000);
        using MemoryStream stream = Utf8(body);

        TextExtractionResult result = await extractor.ExtractAsync(
            stream, "text/markdown", maxCharLength: 100, cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
        result.CharCount.ShouldBe(100);
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions options = new() { MaxBodySizeBytes = 16 };
        MarkdownTextExtractor extractor = CreateExtractor(options);
        using MemoryStream stream = Utf8(new string('a', 4096));

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await extractor.ExtractAsync(
                stream, "text/markdown", maxCharLength: 1024, cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }
}
