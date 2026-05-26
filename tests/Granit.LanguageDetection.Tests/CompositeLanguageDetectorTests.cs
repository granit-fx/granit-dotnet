using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Tests;

public sealed class CompositeLanguageDetectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Higher_priority_provider_wins()
    {
        StubProvider low = new(priority: 100, returns: "fr");
        StubProvider high = new(priority: 200, returns: "en");

        CompositeLanguageDetector composite = new([low, high]);

        string? result = await composite.DetectAsync("anything", Ct);
        result.ShouldBe("en");
        high.CallCount.ShouldBe(1);
        low.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Falls_through_when_high_priority_returns_null()
    {
        StubProvider primary = new(priority: 200, returns: null);
        StubProvider fallback = new(priority: 100, returns: "es");

        CompositeLanguageDetector composite = new([fallback, primary]);

        string? result = await composite.DetectAsync("anything", Ct);
        result.ShouldBe("es");
        primary.CallCount.ShouldBe(1);
        fallback.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Returns_null_when_no_provider_matches()
    {
        StubProvider a = new(priority: 100, returns: null);
        StubProvider b = new(priority: 50, returns: null);
        CompositeLanguageDetector composite = new([a, b]);

        (await composite.DetectAsync("anything", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Empty_string_treated_as_no_detection()
    {
        StubProvider hollow = new(priority: 200, returns: string.Empty);
        StubProvider real = new(priority: 100, returns: "de");
        CompositeLanguageDetector composite = new([hollow, real]);

        (await composite.DetectAsync("anything", Ct)).ShouldBe("de");
    }

    [Fact]
    public void Composite_does_not_implement_provider_marker()
    {
        // Type-system guarantee that a composite cannot accidentally be passed back
        // into another composite's provider list — the bug the runtime filter used to
        // guard against is now impossible to express.
        typeof(ILanguageDetectorProvider).IsAssignableFrom(typeof(CompositeLanguageDetector)).ShouldBeFalse();
    }

    [Fact]
    public void Composite_advertises_max_priority() =>
        new CompositeLanguageDetector([]).Priority.ShouldBe(int.MaxValue);

    private sealed class StubProvider(int priority, string? returns) : ILanguageDetectorProvider
    {
        public int Priority { get; } = priority;
        public int CallCount { get; private set; }

        public Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(returns);
        }
    }
}
