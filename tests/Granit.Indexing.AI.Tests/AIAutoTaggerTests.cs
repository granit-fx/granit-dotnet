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

public sealed class AIAutoTaggerTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task TagAsync_returns_empty_when_content_is_whitespace()
    {
        Harness h = new();
        IReadOnlyList<string> result = await h.Build().TagAsync(
            "   ", Harness.Candidates(["alpha", "beta"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await h.Structured.DidNotReceiveWithAnyArgs().CompleteAsync<AutoTagResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_returns_empty_when_candidate_list_is_empty()
    {
        Harness h = new();
        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates([]), 5, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await h.Structured.DidNotReceiveWithAnyArgs().CompleteAsync<AutoTagResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_returns_subset_of_candidate_universe_on_a_well_formed_response()
    {
        Harness h = new();
        h.RespondWithTags("alpha", "gamma");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["alpha", "beta", "gamma"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "gamma"]);
    }

    [Fact]
    public async Task TagAsync_drops_out_of_candidate_tags_silently_and_emits_metric()
    {
        Harness h = new();
        h.RespondWithTags("alpha", "delete-everything", "beta");
        using MetricCollector<long> oof = new(h.MeterFactory.Meter, "granit.indexing.ai.autotag.out_of_candidate");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["alpha", "beta"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "beta"]);
        oof.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task TagAsync_caps_at_min_of_caller_maxTags_and_options_MaxAutoTagsReturned()
    {
        Harness h = new();
        h.Options.MaxAutoTagsReturned = 10;
        h.RespondWithTags("a", "b", "c", "d", "e");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["a", "b", "c", "d", "e"]), 3, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);

        Harness h2 = new();
        h2.Options.MaxAutoTagsReturned = 2;
        h2.RespondWithTags("a", "b", "c", "d", "e");

        IReadOnlyList<string> result2 = await h2.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["a", "b", "c", "d", "e"]), 100, TestContext.Current.CancellationToken);

        result2.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TagAsync_deduplicates_repeated_LLM_picks()
    {
        Harness h = new();
        h.RespondWithTags("alpha", "alpha", "beta");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["alpha", "beta"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public async Task TagAsync_returns_empty_and_emits_injection_metric_on_schema_violation()
    {
        Harness h = new();
        h.RespondWith(StructuredCompletionStatus.SchemaViolation);
        using MetricCollector<long> injection = new(h.MeterFactory.Meter, "granit.indexing.ai.autotag.injection_attempt");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["alpha"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        injection.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task TagAsync_returns_empty_and_emits_throttled_metric_when_rate_limited()
    {
        Harness h = new();
        h.SaturateRateLimiter();
        using MetricCollector<long> throttled = new(h.MeterFactory.Meter, "granit.indexing.ai.autotag.calls.throttled");

        IReadOnlyList<string> result = await h.Build().TagAsync(
            "lorem ipsum", Harness.Candidates(["alpha"]), 5, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        throttled.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
        await h.Structured.DidNotReceiveWithAnyArgs().CompleteAsync<AutoTagResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness h = new();
        h.RespondWithTags("alpha");

        await h.Build().TagAsync(
            "contact me at jdoe@example.com please", Harness.Candidates(["alpha"]), 5, TestContext.Current.CancellationToken);

        h.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    [Fact]
    public async Task TagAsync_skips_redactor_when_PII_redaction_is_disabled()
    {
        Harness h = new();
        h.Options.RedactPIIBeforeLLMCall = false;
        h.RespondWithTags("alpha");

        await h.Build().TagAsync(
            "contact me at jdoe@example.com please", Harness.Candidates(["alpha"]), 5, TestContext.Current.CancellationToken);

        h.Redactor.DidNotReceive().Redact(Arg.Any<string>());
    }

    private sealed class Harness
    {
        public IStructuredCompletion Structured { get; } = Substitute.For<IStructuredCompletion>();
        public FakeRateLimiter RateLimiter { get; } = new();
        public IAIContentRedactor Redactor { get; } = Substitute.For<IAIContentRedactor>();
        public IndexingAIOptions Options { get; } = new();
        public TestMeterFactory MeterFactory { get; } = new();
        public IndexingAIMetrics Metrics { get; }

        public Harness()
        {
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));
            Metrics = new IndexingAIMetrics(MeterFactory);
        }

        public void RespondWithTags(params string[] tags) =>
            Structured.CompleteAsync<AutoTagResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<AutoTagResponse>
                {
                    Status = StructuredCompletionStatus.Succeeded,
                    Value = new AutoTagResponse { Tags = tags },
                });

        public void RespondWith(StructuredCompletionStatus status) =>
            Structured.CompleteAsync<AutoTagResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
                .Returns(new StructuredCompletionResult<AutoTagResponse> { Status = status });

        public void SaturateRateLimiter() => RateLimiter.Saturated = true;

        public static ITagCandidateProvider Candidates(IReadOnlyList<string> tags)
        {
            ITagCandidateProvider p = Substitute.For<ITagCandidateProvider>();
            p.GetCandidatesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(tags));
            return p;
        }

        public AIAutoTagger Build() => new(
            Structured,
            new DefaultAutoTagPromptBuilder(),
            RateLimiter,
            Redactor,
            Microsoft.Extensions.Options.Options.Create(Options),
            Metrics,
            new FixedTenant(TenantA),
            NullLogger<AIAutoTagger>.Instance);
    }

    private sealed class FakeRateLimiter : IAICallRateLimiter
    {
        public bool Saturated { get; set; }

        public ValueTask<bool> TryAcquireAsync(string bucketKey, int maxCallsPerHour, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!Saturated);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Meter { get; } = new(IndexingAIMetrics.MeterName);
        public Meter Create(MeterOptions options) => Meter;
        public void Dispose() => Meter.Dispose();
    }

    private sealed class FixedTenant(Guid? id) : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; } = id;
        public string? Name => null;
        public string? Jurisdiction => null;
        public IDisposable Change(Guid? id, string? name = null, string? jurisdiction = null) => throw new NotSupportedException();
    }
}
