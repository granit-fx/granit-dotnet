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

public sealed class AIAutoTaggerTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task TagAsync_returns_empty_when_content_is_whitespace()
    {
        // No paid LLM call should be issued for trivial input.
        Harness h = new();
        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "   ",
            Harness.Candidates(["alpha", "beta"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await h.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_returns_empty_when_candidate_list_is_empty()
    {
        // Tenant-onboarding scenario: no tag universe yet → no point calling the LLM.
        Harness h = new();
        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates([]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await h.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_returns_subset_of_candidate_universe_on_a_well_formed_response()
    {
        Harness h = new();
        h.RespondWith("""{"tags": ["alpha", "gamma"]}""");
        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["alpha", "beta", "gamma"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "gamma"]);
    }

    [Fact]
    public async Task TagAsync_drops_out_of_candidate_tags_silently_and_emits_metric()
    {
        // The non-negotiable safety net: the LLM proposes "delete-everything" (which is
        // NOT in the candidate list); the auto-tagger must drop it before the consumer
        // ever sees it. The metric captures the attempt for ops alerting.
        Harness h = new();
        h.RespondWith("""{"tags": ["alpha", "delete-everything", "beta"]}""");

        using MetricCollector<long> oof = new(
            h.MeterFactory.Meter, "granit.indexing.ai.autotag.out_of_candidate");

        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["alpha", "beta"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "beta"]);
        oof.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task TagAsync_caps_at_min_of_caller_maxTags_and_options_MaxAutoTagsReturned()
    {
        // Two caps — the smaller wins. Caller wants 3, options allows 10, we cap at 3.
        Harness h = new();
        h.Options.MaxAutoTagsReturned = 10;
        h.RespondWith("""{"tags": ["a", "b", "c", "d", "e"]}""");
        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["a", "b", "c", "d", "e"]),
            maxTags: 3,
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);

        // Inverse: caller wants 100, options caps at 2.
        Harness h2 = new();
        h2.Options.MaxAutoTagsReturned = 2;
        h2.RespondWith("""{"tags": ["a", "b", "c", "d", "e"]}""");
        AIAutoTagger tagger2 = h2.Build();

        IReadOnlyList<string> result2 = await tagger2.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["a", "b", "c", "d", "e"]),
            maxTags: 100,
            TestContext.Current.CancellationToken);

        result2.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TagAsync_deduplicates_repeated_LLM_picks()
    {
        // Defensive: if the LLM repeats a tag (which it shouldn't but real-world bugs
        // happen), the auto-tagger returns each only once.
        Harness h = new();
        h.RespondWith("""{"tags": ["alpha", "alpha", "beta"]}""");
        AIAutoTagger tagger = h.Build();

        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["alpha", "beta"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public async Task TagAsync_returns_empty_and_emits_injection_metric_when_response_is_unparsable_json()
    {
        Harness h = new();
        h.RespondWith("not json at all");

        using MetricCollector<long> injection = new(
            h.MeterFactory.Meter, "granit.indexing.ai.autotag.injection_attempt");

        AIAutoTagger tagger = h.Build();
        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["alpha"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        injection.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task TagAsync_returns_empty_and_emits_throttled_metric_when_rate_limited()
    {
        // Per-tenant cost ceiling: a flooded tenant must NOT block the indexing pipeline.
        Harness h = new();
        h.SaturateRateLimiter();

        using MetricCollector<long> throttled = new(
            h.MeterFactory.Meter, "granit.indexing.ai.autotag.calls.throttled");

        AIAutoTagger tagger = h.Build();
        IReadOnlyList<string> result = await tagger.TagAsync(
            "lorem ipsum",
            Harness.Candidates(["alpha"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        throttled.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
        await h.ChatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TagAsync_routes_content_through_redactor_when_PII_redaction_is_enabled()
    {
        Harness h = new();
        h.RespondWith("""{"tags": ["alpha"]}""");
        AIAutoTagger tagger = h.Build();

        await tagger.TagAsync(
            "contact me at jdoe@example.com please",
            Harness.Candidates(["alpha"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        h.Redactor.Received(1).Redact(Arg.Any<string>());
    }

    [Fact]
    public async Task TagAsync_skips_redactor_when_PII_redaction_is_disabled()
    {
        Harness h = new();
        h.Options.RedactPIIBeforeLLMCall = false;
        h.RespondWith("""{"tags": ["alpha"]}""");
        AIAutoTagger tagger = h.Build();

        await tagger.TagAsync(
            "contact me at jdoe@example.com please",
            Harness.Candidates(["alpha"]),
            maxTags: 5,
            TestContext.Current.CancellationToken);

        h.Redactor.DidNotReceive().Redact(Arg.Any<string>());
    }

    private sealed class Harness
    {
        public IAIChatClientFactory ChatClientFactory { get; } = Substitute.For<IAIChatClientFactory>();
        public IChatClient ChatClient { get; } = Substitute.For<IChatClient>();
        public FakeRateLimiter RateLimiter { get; } = new();
        public IAIContentRedactor Redactor { get; } = Substitute.For<IAIContentRedactor>();
        public IndexingAIOptions Options { get; } = new();
        public TestMeterFactory MeterFactory { get; } = new();
        public IndexingAIMetrics Metrics { get; }

        public Harness()
        {
            ChatClientFactory.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(ChatClient));
            Redactor.Redact(Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));
            Metrics = new IndexingAIMetrics(MeterFactory);
        }

        public void RespondWith(string text)
        {
            ChatResponse response = new(new ChatMessage(ChatRole.Assistant, text));
            ChatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(response));
        }

        public void SaturateRateLimiter() => RateLimiter.Saturated = true;

        public static ITagCandidateProvider Candidates(IReadOnlyList<string> tags)
        {
            ITagCandidateProvider p = Substitute.For<ITagCandidateProvider>();
            p.GetCandidatesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(tags));
            return p;
        }

        public AIAutoTagger Build() => new(
            ChatClientFactory,
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
        public IDisposable Change(Guid? id, string? name = null) => throw new NotSupportedException();
    }
}
