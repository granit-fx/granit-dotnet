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
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("plain body");
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
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("ignored");
        TextExtractionResult htmlResult = await pipeline.ExtractAsync(
            stream,
            "text/html",
            TestContext.Current.CancellationToken);
        htmlResult.ExtractorName.ShouldBe(FakeHtmlExtractor.Id);

        await using MemoryStream stream2 = Utf8("ignored");
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
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("body");
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
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("hello world");
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
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(s => s.AddTextExtractor<ThrowingExtractor>());
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("anything");
        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await pipeline.ExtractAsync(
                stream,
                "application/x-throws",
                TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public async Task ExtractionTimeout_translates_slow_extractor_to_extraction_timeout_failure()
    {
        // A slow parser must surface as a structured failure, not pin a slot.
        ExtractionOptions options = new() { ExtractionTimeout = TimeSpan.FromMilliseconds(50) };
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(
            s => s.AddTextExtractor<SlowExtractor>(),
            options);
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("ignored");

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => await pipeline.ExtractAsync(
                stream,
                "application/x-slow",
                TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("extraction_timeout");
    }

    [Fact]
    public async Task ExtractionTimeout_zero_disables_the_per_call_deadline()
    {
        // Hosts can opt down with TimeSpan.Zero for offline batch jobs.
        ExtractionOptions options = new() { ExtractionTimeout = TimeSpan.Zero };
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(
            s => s.AddTextExtractor<FakeHtmlExtractor>(),
            options);
        await using ServiceProvider _ = sp;

        await using MemoryStream stream = Utf8("ignored");
        TextExtractionResult result = await pipeline.ExtractAsync(
            stream, "text/html", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(FakeHtmlExtractor.Id);
    }

    [Fact]
    public async Task MaxConcurrentExtractions_caps_in_flight_calls()
    {
        // The (N+1)th call must queue behind the first N slots.
        ExtractionOptions options = new()
        {
            MaxConcurrentExtractions = 2,
            ExtractionTimeout = TimeSpan.FromSeconds(10),
        };
        (ITextExtractionPipeline pipeline, ServiceProvider sp) = BuildPipeline(
            s => s.AddTextExtractor<GateableExtractor>(),
            options);
        await using ServiceProvider _ = sp;

        // 3 concurrent calls; only 2 should be in-flight at any moment.
        Task<TextExtractionResult>[] inFlight =
        [
            Run(pipeline),
            Run(pipeline),
            Run(pipeline),
        ];

        // Give the first two a chance to enter the gate before asserting.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        GateableExtractor.CurrentInFlight.ShouldBeLessThanOrEqualTo(2);
        GateableExtractor.MaxObservedInFlight.ShouldBeLessThanOrEqualTo(2);

        // Release in waves: the third should only start after one finishes.
        GateableExtractor.ReleaseOne();
        GateableExtractor.ReleaseOne();
        GateableExtractor.ReleaseOne();

        await Task.WhenAll(inFlight);
        GateableExtractor.MaxObservedInFlight.ShouldBeLessThanOrEqualTo(2);

        static Task<TextExtractionResult> Run(ITextExtractionPipeline p)
        {
            MemoryStream s = new(Encoding.UTF8.GetBytes("x"));
            return p.ExtractAsync(s, "application/x-gateable", CancellationToken.None);
        }
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
            Task.FromResult(new TextExtractionResult("h", null, false, 1, Id, ExtractionConfidence.Deterministic));
    }

    private sealed class FakeMarkdownExtractor : ITextExtractor
    {
        public const string Id = "granit.text-extraction.fake-md";

        public string Name => Id;

        public bool CanHandle(string contentType) =>
            contentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase);

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            Task.FromResult(new TextExtractionResult("m", null, false, 1, Id, ExtractionConfidence.Deterministic));
    }

    private sealed class FakeCatchAllExtractor : ITextExtractor
    {
        public const string Id = "granit.text-extraction.fake-catchall";

        public string Name => Id;

        public bool CanHandle(string contentType) => true;

        public Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken) =>
            Task.FromResult(new TextExtractionResult("c", null, false, 1, Id, ExtractionConfidence.Deterministic));
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

    private sealed class SlowExtractor : ITextExtractor
    {
        public string Name => "granit.text-extraction.slow";

        public bool CanHandle(string contentType) =>
            contentType.Equals("application/x-slow", StringComparison.OrdinalIgnoreCase);

        public async Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken)
        {
            // Sleep > pipeline timeout. Honours the linked token so the pipeline can pre-empt.
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return new TextExtractionResult("never", null, false, 5, Name, ExtractionConfidence.Deterministic);
        }
    }

    /// <summary>
    /// Test double that lets the unit test orchestrate exactly when each in-flight
    /// extraction finishes — proves the pipeline's concurrency gate caps in-flight calls.
    /// </summary>
    private sealed class GateableExtractor : ITextExtractor
    {
        private static readonly SemaphoreSlim Release = new(0, int.MaxValue);
        private static int _inFlight;
        private static int _maxObservedInFlight;
        private static readonly Lock Sync = new();

        public string Name => "granit.text-extraction.gateable";

        public bool CanHandle(string contentType) =>
            contentType.Equals("application/x-gateable", StringComparison.OrdinalIgnoreCase);

        public async Task<TextExtractionResult> ExtractAsync(
            Stream source, string contentType, int maxCharLength, CancellationToken cancellationToken)
        {
            lock (Sync)
            {
                _inFlight++;
                if (_inFlight > _maxObservedInFlight)
                {
                    _maxObservedInFlight = _inFlight;
                }
            }

            try
            {
                await Release.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new TextExtractionResult("g", null, false, 1, Name, ExtractionConfidence.Deterministic);
            }
            finally
            {
                lock (Sync) { _inFlight--; }
            }
        }

        public static int CurrentInFlight { get { lock (Sync) { return _inFlight; } } }
        public static int MaxObservedInFlight { get { lock (Sync) { return _maxObservedInFlight; } } }
        public static void ReleaseOne() => Release.Release();
    }
}
