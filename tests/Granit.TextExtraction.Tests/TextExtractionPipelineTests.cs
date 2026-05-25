using System.Text;
using Granit.TextExtraction.Exceptions;
using Granit.TextExtraction.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Tests;

public sealed class TextExtractionPipelineTests
{
    private static (ITextExtractionPipeline pipeline, ServiceProvider sp) BuildPipeline(
        Action<IServiceCollection>? configure = null,
        ExtractionOptions? options = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IMeterFactoryAlias>(_ => new IMeterFactoryAlias());
        services.AddSingleton<System.Diagnostics.Metrics.IMeterFactory, TestMeterFactory>();
        services.AddSingleton(MEOptions.Create(options ?? new ExtractionOptions()));
        services.AddGranitTextExtraction();

        configure?.Invoke(services);

        ServiceProvider sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<ITextExtractionPipeline>(), sp);
    }

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Falls_back_to_plain_text_when_no_extractor_claims_content_type()
    {
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline();
        using ServiceProvider _ = sp;

        using MemoryStream stream = Utf8("plain body");
        TextExtractionResult result = await pipeline.ExtractAsync(
            stream,
            "application/x-unknown",
            TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(PlainTextExtractor.ExtractorName);
        result.Content.ShouldBe("plain body");
    }

    [Fact]
    public async Task Routes_to_first_matching_registered_extractor()
    {
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(s =>
        {
            s.AddTextExtractor<FakeHtmlExtractor>();
            s.AddTextExtractor<FakeMarkdownExtractor>();
        });
        using ServiceProvider _ = sp;

        using MemoryStream stream = Utf8("ignored");
        TextExtractionResult htmlResult = await pipeline.ExtractAsync(
            stream,
            "text/html",
            TestContext.Current.CancellationToken);
        htmlResult.ExtractorName.ShouldBe(FakeHtmlExtractor.Id);

        using MemoryStream stream2 = Utf8("ignored");
        TextExtractionResult mdResult = await pipeline.ExtractAsync(
            stream2,
            "text/markdown",
            TestContext.Current.CancellationToken);
        mdResult.ExtractorName.ShouldBe(FakeMarkdownExtractor.Id);
    }

    [Fact]
    public async Task First_registered_wins_even_when_a_later_one_also_handles()
    {
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(s =>
        {
            // FakeHtmlExtractor handles text/html; registered first.
            s.AddTextExtractor<FakeHtmlExtractor>();
            s.AddTextExtractor<FakeCatchAllExtractor>();
        });
        using ServiceProvider _ = sp;

        using MemoryStream stream = Utf8("body");
        TextExtractionResult result = await pipeline.ExtractAsync(
            stream,
            "text/html",
            TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(FakeHtmlExtractor.Id);
    }

    [Fact]
    public async Task Honours_max_extracted_char_length_via_options()
    {
        ExtractionOptions options = new() { MaxExtractedCharLength = 5 };
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(options: options);
        using ServiceProvider _ = sp;

        using MemoryStream stream = Utf8("hello world");
        TextExtractionResult result = await pipeline.ExtractAsync(
            stream,
            "text/plain",
            TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(5);
    }

    [Fact]
    public async Task Propagates_text_extraction_exception_from_extractor()
    {
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(s =>
        {
            s.AddTextExtractor<ThrowingExtractor>();
        });
        using ServiceProvider _ = sp;

        using MemoryStream stream = Utf8("anything");
        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await pipeline.ExtractAsync(
                stream,
                "application/x-throws",
                TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    // ──── Fakes ────

    private sealed class FakeHtmlExtractor : ITextExtractor
    {
        public const string Id = "granit.text-extraction.fake-html";

        public string Name => Id;

        public bool CanHandle(string contentType) =>
            contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase);

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            Task.FromResult(new TextExtractionResult("h", null, false, 1, Id));
    }

    private sealed class FakeMarkdownExtractor : ITextExtractor
    {
        public const string Id = "granit.text-extraction.fake-md";

        public string Name => Id;

        public bool CanHandle(string contentType) =>
            contentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase);

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            Task.FromResult(new TextExtractionResult("m", null, false, 1, Id));
    }

    private sealed class FakeCatchAllExtractor : ITextExtractor
    {
        public const string Id = "granit.text-extraction.fake-catchall";

        public string Name => Id;

        public bool CanHandle(string contentType) => true;

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            Task.FromResult(new TextExtractionResult("c", null, false, 1, Id));
    }

    private sealed class ThrowingExtractor : ITextExtractor
    {
        public string Name => "granit.text-extraction.throwing";

        public bool CanHandle(string contentType) =>
            contentType.Equals("application/x-throws", StringComparison.OrdinalIgnoreCase);

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            throw new TextExtractionException("input_too_large");
    }

    private sealed class IMeterFactoryAlias
    {
        // Anchor — kept solely to ensure the meter factory binding above doesn't get pruned.
    }
}
