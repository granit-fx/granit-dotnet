using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.AI.Redaction;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Granit.Testing.Fakes;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.AI.Tests;

public sealed class AILanguageDetectorTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Priority_is_locked_to_200_so_it_wins_over_the_pure_managed_Trigram_default()
    {
        AILanguageDetector detector = BuildDetector(new Harness());

        detector.Priority.ShouldBe(200);
    }

    [Fact]
    public async Task DetectAsync_returns_null_when_content_is_below_the_minimum_sample_length()
    {
        Harness harness = new();
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("abc", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await harness.Structured.DidNotReceiveWithAnyArgs()
            .CompleteAsync<LanguageDetectionResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DetectAsync_returns_the_iso_code_on_a_well_formed_response()
    {
        Harness harness = new();
        harness.RespondWithLanguage("fr");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBe("fr");
    }

    [Fact]
    public async Task DetectAsync_lowercases_the_iso_code_so_callers_can_compare_directly()
    {
        Harness harness = new();
        harness.RespondWithLanguage("FR");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBe("fr");
    }

    [Fact]
    public async Task DetectAsync_returns_null_and_emits_injection_metric_when_response_violates_iso_639_1_pattern()
    {
        Harness harness = new();
        harness.RespondWithLanguage("fra");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_returns_null_and_emits_injection_metric_on_schema_violation()
    {
        // An out-of-schema / unparseable model response surfaces from the primitive as
        // SchemaViolation — the detector treats it as a rejected (possibly injected) payload.
        Harness harness = new();
        harness.RespondWith(StructuredCompletionStatus.SchemaViolation);
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_skips_call_and_emits_throttled_metric_when_rate_limited()
    {
        Harness harness = new();
        harness.SaturateRateLimiter();
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectThrottledCount().ShouldBe(1);
        await harness.Structured.DidNotReceiveWithAnyArgs()
            .CompleteAsync<LanguageDetectionResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DetectAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness harness = new();
        harness.RespondWithLanguage("en");
        AILanguageDetector detector = BuildDetector(harness);

        await detector.DetectAsync("contact me at jdoe@example.com please", TestContext.Current.CancellationToken);

        harness.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    [Fact]
    public async Task DetectAsync_skips_redactor_when_PII_redaction_is_disabled()
    {
        Harness harness = new();
        harness.Options.RedactPIIBeforeLLMCall = false;
        harness.RespondWithLanguage("en");
        AILanguageDetector detector = BuildDetector(harness);

        await detector.DetectAsync("contact me at jdoe@example.com please", TestContext.Current.CancellationToken);

        harness.Redactor.DidNotReceive().Redact(Arg.Any<string>());
    }

    [Fact]
    public async Task DetectAsync_truncates_to_MaxContentLength_without_splitting_a_surrogate_pair()
    {
        Harness harness = new();
        harness.Options.MaxContentLength = 12;
        harness.RespondWithLanguage("en");
        AILanguageDetector detector = BuildDetector(harness);

        // 🚀 = U+1F680 (surrogate pair). Cutting at 12 would split it; back-off trims to 11.
        await detector.DetectAsync("abcdefghijk🚀tail", TestContext.Current.CancellationToken);

        // The Content sent to the primitive is the (truncated) sample — the primitive owns
        // the <data> isolation envelope.
        string content = harness.LastRequest.ShouldNotBeNull().Content.ShouldNotBeNull();
        content.ShouldNotMatch(@"[\uD800-\uDBFF](?![\uDC00-\uDFFF])");
        content.ShouldNotMatch(@"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]");
        content.Length.ShouldBeLessThanOrEqualTo(harness.Options.MaxContentLength);
        content.ShouldBe("abcdefghijk");
    }

    [Fact]
    public async Task DetectAsync_records_transport_failure_when_completion_fails()
    {
        Harness harness = new();
        harness.RespondWith(StructuredCompletionStatus.TransportFailure);
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectFailedCount().ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_passes_the_builder_instruction_as_the_request_instruction()
    {
        Harness harness = new();
        harness.RespondWithLanguage("en");
        AILanguageDetector detector = BuildDetector(harness);

        await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        harness.LastRequest.ShouldNotBeNull().Instruction!.ShouldContain("ISO 639-1");
    }

    private static AILanguageDetector BuildDetector(Harness h) => new(
        h.Structured,
        new DefaultAILanguageDetectionPromptBuilder(),
        h.RateLimiter,
        h.Redactor,
        Microsoft.Extensions.Options.Options.Create(h.Options),
        h.Metrics,
        new FakeCurrentTenant { Id = TenantA },
        NullLogger<AILanguageDetector>.Instance);

    private sealed class Harness
    {
        public IStructuredCompletion Structured { get; } = Substitute.For<IStructuredCompletion>();
        public FakeRateLimiter RateLimiter { get; } = new();
        public IAIContentRedactor Redactor { get; } = Substitute.For<IAIContentRedactor>();
        public LanguageDetectionAIOptions Options { get; } = new();
        public MetricCollector<long> ThrottledCollector { get; }
        public MetricCollector<long> InjectionCollector { get; }
        public MetricCollector<long> FailedCollector { get; }
        public LanguageDetectionAIMetrics Metrics { get; }
        public StructuredCompletionRequest? LastRequest { get; private set; }

        public Harness()
        {
            var meterFactory = new TestMeterFactory();
            Metrics = new LanguageDetectionAIMetrics(meterFactory);
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));

            ThrottledCollector = new MetricCollector<long>(meterFactory.Meter, "granit.language_detection.ai.calls.throttled");
            InjectionCollector = new MetricCollector<long>(meterFactory.Meter, "granit.language_detection.ai.injections.detected");
            FailedCollector = new MetricCollector<long>(meterFactory.Meter, "granit.language_detection.ai.calls.failed");
        }

        public void RespondWithLanguage(string language) =>
            Structured
                .CompleteAsync<LanguageDetectionResponse>(Arg.Do<StructuredCompletionRequest>(r => LastRequest = r), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<LanguageDetectionResponse>
                {
                    Status = StructuredCompletionStatus.Succeeded,
                    Value = new LanguageDetectionResponse { Language = language },
                });

        public void RespondWith(StructuredCompletionStatus status) =>
            Structured
                .CompleteAsync<LanguageDetectionResponse>(Arg.Do<StructuredCompletionRequest>(r => LastRequest = r), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<LanguageDetectionResponse> { Status = status });

        public void SaturateRateLimiter() => RateLimiter.Saturated = true;

        public long CollectThrottledCount() => ThrottledCollector.GetMeasurementSnapshot().Sum(m => m.Value);
        public long CollectInjectionCount() => InjectionCollector.GetMeasurementSnapshot().Sum(m => m.Value);
        public long CollectFailedCount() => FailedCollector.GetMeasurementSnapshot().Sum(m => m.Value);
    }

    private sealed class FakeRateLimiter : IAICallRateLimiter
    {
        public bool Saturated { get; set; }

        public ValueTask<bool> TryAcquireAsync(string bucketKey, int maxCallsPerHour, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!Saturated);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Meter { get; } = new("Granit.LanguageDetection.AI");
        public Meter Create(MeterOptions options) => Meter;
        public void Dispose() => Meter.Dispose();
    }

}
