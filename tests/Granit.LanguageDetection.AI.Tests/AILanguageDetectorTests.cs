using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.Redaction;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
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
        // The whole composite-chain contract hinges on this number. If we silently move
        // it to a lower priority, hosts that wire both providers would start getting
        // Trigram answers first, paying the LLM cost as a fallback — the opposite of
        // what they configured.
        AILanguageDetector detector = BuildDetector(new Harness());

        detector.Priority.ShouldBe(200);
    }

    [Fact]
    public async Task DetectAsync_returns_null_when_content_is_below_the_minimum_sample_length()
    {
        // Short snippets don't justify a paid LLM call; skipping here also avoids
        // burning a rate-limit token. The composite falls through to Trigram which has
        // its own short-input guard.
        Harness harness = new();
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("abc", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await harness.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DetectAsync_returns_the_iso_code_on_a_well_formed_response()
    {
        Harness harness = new();
        harness.RespondWith("""{"language": "fr"}""");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBe("fr");
    }

    [Fact]
    public async Task DetectAsync_lowercases_the_iso_code_so_callers_can_compare_directly()
    {
        Harness harness = new();
        harness.RespondWith("""{"language": "FR"}""");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBe("fr");
    }

    [Fact]
    public async Task DetectAsync_returns_null_and_emits_injection_metric_when_response_violates_iso_639_1_pattern()
    {
        // Three-letter codes, three-digit codes, and "code switch" tokens are all common
        // prompt-injection probes. The regex validator rejects them and the call is
        // logged as an injection attempt (tenant-tagged) without leaking the rejected
        // payload to the metric.
        Harness harness = new();
        harness.RespondWith("""{"language": "fra"}""");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_returns_null_and_emits_injection_metric_when_response_is_unparsable_json()
    {
        Harness harness = new();
        harness.RespondWith("not json at all");
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_skips_call_and_emits_throttled_metric_when_rate_limited()
    {
        // A flooded tenant must NOT block the indexing pipeline. The detector returns
        // null gracefully, bumps a tenant-tagged counter, and the composite falls
        // through to the next provider — never throws.
        Harness harness = new();
        harness.SaturateRateLimiter();
        AILanguageDetector detector = BuildDetector(harness);

        string? result = await detector.DetectAsync("Bonjour à tous, ceci est un texte en français.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectThrottledCount().ShouldBe(1);
        await harness.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DetectAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness harness = new();
        harness.RespondWith("""{"language": "en"}""");
        AILanguageDetector detector = BuildDetector(harness);

        await detector.DetectAsync("contact me at jdoe@example.com please", TestContext.Current.CancellationToken);

        harness.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    [Fact]
    public async Task DetectAsync_skips_redactor_when_PII_redaction_is_disabled()
    {
        Harness harness = new();
        harness.Options.RedactPIIBeforeLLMCall = false;
        harness.RespondWith("""{"language": "en"}""");
        AILanguageDetector detector = BuildDetector(harness);

        await detector.DetectAsync("contact me at jdoe@example.com please", TestContext.Current.CancellationToken);

        harness.Redactor.DidNotReceive().Redact(Arg.Any<string>());
    }

    private static AILanguageDetector BuildDetector(Harness h) => new(
        h.ChatClientFactory,
        new DefaultAILanguageDetectionPromptBuilder(),
        h.RateLimiter,
        h.Redactor,
        Microsoft.Extensions.Options.Options.Create(h.Options),
        h.Metrics,
        new FixedTenant(TenantA),
        NullLogger<AILanguageDetector>.Instance);

    private sealed class Harness
    {
        public IAIChatClientFactory ChatClientFactory { get; } = Substitute.For<IAIChatClientFactory>();
        public IChatClient ChatClient { get; } = Substitute.For<IChatClient>();

        // Hand-rolled fake to sidestep the NSubstitute + ValueTask + CA2012 friction —
        // a tiny class is clearer than a `Returns(_ => new ValueTask<bool>(...))` dance.
        public FakeRateLimiter RateLimiter { get; } = new();
        public IAIContentRedactor Redactor { get; } = Substitute.For<IAIContentRedactor>();
        public LanguageDetectionAIOptions Options { get; } = new();
        public MetricCollector<long> ThrottledCollector { get; }
        public MetricCollector<long> InjectionCollector { get; }
        public LanguageDetectionAIMetrics Metrics { get; }

        public Harness()
        {
            var meterFactory = new TestMeterFactory();
            Metrics = new LanguageDetectionAIMetrics(meterFactory);
            ChatClientFactory.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(ChatClient));
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));

            ThrottledCollector = new MetricCollector<long>(
                meterFactory.Meter, "granit.language_detection.ai.calls.throttled");
            InjectionCollector = new MetricCollector<long>(
                meterFactory.Meter, "granit.language_detection.ai.injection_attempt");
        }

        public void RespondWith(string text)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            ChatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(response));
        }

        public void SaturateRateLimiter() => RateLimiter.Saturated = true;

        public long CollectThrottledCount() => ThrottledCollector.GetMeasurementSnapshot().Sum(m => m.Value);
        public long CollectInjectionCount() => InjectionCollector.GetMeasurementSnapshot().Sum(m => m.Value);
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

    private sealed class FixedTenant(Guid? id) : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; } = id;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => throw new NotSupportedException();
    }
}
