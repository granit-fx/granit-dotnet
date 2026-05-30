using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.AI.Redaction;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Internal;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Indexing.AI.Tests;

public sealed class AISummarizerTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SummarizeAsync_returns_null_when_content_is_empty()
    {
        Harness harness = new();
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync("   ", language: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SummarizeAsync_returns_summary_on_successful_call()
    {
        Harness harness = new();
        harness.RespondWith("""{"summary": "A short summary."}""");
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync(
            "Long document body that the LLM will boil down for SERP display.",
            language: "en",
            TestContext.Current.CancellationToken);

        result.ShouldBe("A short summary.");
    }

    [Fact]
    public async Task SummarizeAsync_truncates_and_records_metric_when_response_exceeds_cap()
    {
        // The schema cap is enforced server-side AND client-side: even if the LLM ignores
        // the prompt's length constraint, the summarizer never returns a longer string
        // than MaxSummaryLength. The truncated metric lets ops alert on prompt-adherence
        // drift without parsing log payloads.
        Harness harness = new();
        harness.Options.MaxSummaryLength = 20;
        harness.RespondWith("""{"summary": "this is way longer than twenty characters of output"}""");
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync(
            "Long body to summarize",
            language: null,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Length.ShouldBe(20);
        harness.CollectTruncatedCount().ShouldBe(1);
    }

    [Fact]
    public async Task SummarizeAsync_skips_call_and_emits_throttled_metric_when_rate_limited()
    {
        Harness harness = new();
        harness.RateLimiter.Saturated = true;
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync(
            "Long body to summarize that would normally trigger a call",
            language: null,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectThrottledCount().ShouldBe(1);
        await harness.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SummarizeAsync_returns_null_and_emits_injection_metric_when_response_is_unparsable_json()
    {
        Harness harness = new();
        harness.RespondWith("absolutely not json");
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync("body", language: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task SummarizeAsync_returns_null_when_model_returns_empty_summary()
    {
        // An empty summary is a valid model decision (e.g. document is gibberish). The
        // summarizer treats it as "no summary available" rather than persisting an empty
        // string into IndexedEntry.Summary.
        Harness harness = new();
        harness.RespondWith("""{"summary": ""}""");
        AISummarizer summarizer = BuildSummarizer(harness);

        string? result = await summarizer.SummarizeAsync("body", language: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SummarizeAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness harness = new();
        harness.RespondWith("""{"summary": "ok"}""");
        AISummarizer summarizer = BuildSummarizer(harness);

        await summarizer.SummarizeAsync(
            "Contact me at jdoe@example.com please.",
            language: null,
            TestContext.Current.CancellationToken);

        harness.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    private static AISummarizer BuildSummarizer(Harness h) => new(
        h.ChatClientFactory,
        new DefaultAIAutoSummaryPromptBuilder(),
        h.RateLimiter,
        h.Redactor,
        Microsoft.Extensions.Options.Options.Create(h.Options),
        h.Metrics,
        new FixedTenant(TenantA),
        NullLogger<AISummarizer>.Instance);

    private sealed class Harness
    {
        public IAIChatClientFactory ChatClientFactory { get; } = Substitute.For<IAIChatClientFactory>();
        public IChatClient ChatClient { get; } = Substitute.For<IChatClient>();
        public FakeRateLimiter RateLimiter { get; } = new();
        public IAIContentRedactor Redactor { get; } = Substitute.For<IAIContentRedactor>();
        public IndexingAIOptions Options { get; } = new();
        public MetricCollector<long> ThrottledCollector { get; }
        public MetricCollector<long> InjectionCollector { get; }
        public MetricCollector<long> TruncatedCollector { get; }
        public IndexingAIMetrics Metrics { get; }

        public Harness()
        {
            var meterFactory = new TestMeterFactory();
            Metrics = new IndexingAIMetrics(meterFactory);
            ChatClientFactory.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(ChatClient));
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));

            ThrottledCollector = new MetricCollector<long>(
                meterFactory.Meter, "granit.indexing.ai.summarizer.calls.throttled");
            InjectionCollector = new MetricCollector<long>(
                meterFactory.Meter, "granit.indexing.ai.summarizer.injection_attempt");
            TruncatedCollector = new MetricCollector<long>(
                meterFactory.Meter, "granit.indexing.ai.summarizer.truncated");
        }

        public void RespondWith(string text)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            ChatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(response));
        }

        public long CollectThrottledCount() => ThrottledCollector.GetMeasurementSnapshot().Sum(m => m.Value);
        public long CollectInjectionCount() => InjectionCollector.GetMeasurementSnapshot().Sum(m => m.Value);
        public long CollectTruncatedCount() => TruncatedCollector.GetMeasurementSnapshot().Sum(m => m.Value);
    }

    private sealed class FakeRateLimiter : IAICallRateLimiter
    {
        public bool Saturated { get; set; }

        public ValueTask<bool> TryAcquireAsync(string bucketKey, int maxCallsPerHour, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!Saturated);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Meter { get; } = new("Granit.Indexing.AI");
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
