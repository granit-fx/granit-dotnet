using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Tests;

public sealed class CompositeLanguageDetectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Higher_priority_detector_wins()
    {
        StubDetector low = new(priority: 100, returns: "fr");
        StubDetector high = new(priority: 200, returns: "en");

        CompositeLanguageDetector composite = new([low, high]);

        string? result = await composite.DetectAsync("anything", Ct);
        result.ShouldBe("en");
        high.CallCount.ShouldBe(1);
        low.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Falls_through_when_high_priority_returns_null()
    {
        StubDetector primary = new(priority: 200, returns: null);
        StubDetector fallback = new(priority: 100, returns: "es");

        CompositeLanguageDetector composite = new([fallback, primary]);

        string? result = await composite.DetectAsync("anything", Ct);
        result.ShouldBe("es");
        primary.CallCount.ShouldBe(1);
        fallback.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Returns_null_when_no_detector_matches()
    {
        StubDetector a = new(priority: 100, returns: null);
        StubDetector b = new(priority: 50, returns: null);
        CompositeLanguageDetector composite = new([a, b]);

        (await composite.DetectAsync("anything", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Empty_string_treated_as_no_detection()
    {
        StubDetector hollow = new(priority: 200, returns: string.Empty);
        StubDetector real = new(priority: 100, returns: "de");
        CompositeLanguageDetector composite = new([hollow, real]);

        (await composite.DetectAsync("anything", Ct)).ShouldBe("de");
    }

    [Fact]
    public async Task Excludes_nested_composite_to_avoid_cycle()
    {
        StubDetector inner = new(priority: 100, returns: "fr");
        CompositeLanguageDetector innerComposite = new([inner]);
        StubDetector outer = new(priority: 50, returns: "en");
        CompositeLanguageDetector composite = new([innerComposite, outer]);

        (await composite.DetectAsync("anything", Ct)).ShouldBe("en");
        inner.CallCount.ShouldBe(0);
    }

    [Fact]
    public void Composite_advertises_max_priority() =>
        new CompositeLanguageDetector([]).Priority.ShouldBe(int.MaxValue);

    private sealed class StubDetector(int priority, string? returns) : ILanguageDetector
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
