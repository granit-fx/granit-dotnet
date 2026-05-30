using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.AI.Redaction;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Internal;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Granit.Indexing.AI.Schema;
using Granit.MultiTenancy;
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
        string? result = await BuildSummarizer(harness).SummarizeAsync("   ", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SummarizeAsync_returns_summary_on_successful_call()
    {
        Harness harness = new();
        harness.RespondWithSummary("A short summary.");

        string? result = await BuildSummarizer(harness).SummarizeAsync(
            "Long document body that the LLM will boil down for SERP display.", "en", TestContext.Current.CancellationToken);

        result.ShouldBe("A short summary.");
    }

    [Fact]
    public async Task SummarizeAsync_truncates_and_records_metric_when_response_exceeds_cap()
    {
        Harness harness = new();
        harness.Options.MaxSummaryLength = 20;
        harness.RespondWithSummary("this is way longer than twenty characters of output");

        string? result = await BuildSummarizer(harness).SummarizeAsync("Long body to summarize", null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Length.ShouldBe(20);
        harness.CollectTruncatedCount().ShouldBe(1);
    }

    [Fact]
    public async Task SummarizeAsync_skips_call_and_emits_throttled_metric_when_rate_limited()
    {
        Harness harness = new();
        harness.RateLimiter.Saturated = true;

        string? result = await BuildSummarizer(harness).SummarizeAsync(
            "Long body to summarize that would normally trigger a call", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectThrottledCount().ShouldBe(1);
        await harness.Structured.DidNotReceiveWithAnyArgs().CompleteAsync<SummaryResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SummarizeAsync_returns_null_and_emits_injection_metric_on_schema_violation()
    {
        Harness harness = new();
        harness.RespondWith(StructuredCompletionStatus.SchemaViolation);

        string? result = await BuildSummarizer(harness).SummarizeAsync("body", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        harness.CollectInjectionCount().ShouldBe(1);
    }

    [Fact]
    public async Task SummarizeAsync_returns_null_when_model_returns_empty_summary()
    {
        Harness harness = new();
        harness.RespondWithSummary("");

        string? result = await BuildSummarizer(harness).SummarizeAsync("body", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SummarizeAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness harness = new();
        harness.RespondWithSummary("ok");

        await BuildSummarizer(harness).SummarizeAsync("Contact me at jdoe@example.com please.", null, TestContext.Current.CancellationToken);

        harness.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    private static AISummarizer BuildSummarizer(Harness h) => new(
        h.Structured,
        new DefaultAIAutoSummaryPromptBuilder(),
        h.RateLimiter,
        h.Redactor,
        Microsoft.Extensions.Options.Options.Create(h.Options),
        h.Metrics,
        new FixedTenant(TenantA),
        NullLogger<AISummarizer>.Instance);

    private sealed class Harness
    {
        public IStructuredCompletion Structured { get; } = Substitute.For<IStructuredCompletion>();
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
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));

            ThrottledCollector = new MetricCollector<long>(meterFactory.Meter, "granit.indexing.ai.summarizer.calls.throttled");
            InjectionCollector = new MetricCollector<long>(meterFactory.Meter, "granit.indexing.ai.summarizer.injection_attempt");
            TruncatedCollector = new MetricCollector<long>(meterFactory.Meter, "granit.indexing.ai.summarizer.truncated");
        }

        public void RespondWithSummary(string summary) =>
            Structured.CompleteAsync<SummaryResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<SummaryResponse>
                {
                    Status = StructuredCompletionStatus.Succeeded,
                    Value = new SummaryResponse { Summary = summary },
                });

        public void RespondWith(StructuredCompletionStatus status) =>
            Structured.CompleteAsync<SummaryResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<SummaryResponse> { Status = status });

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
