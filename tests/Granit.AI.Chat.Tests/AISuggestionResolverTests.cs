using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Suggestions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class AISuggestionResolverTests
{
    private static readonly AISuggestionContext Context = new(Guid.NewGuid(), "hello");

    private sealed class StubProvider(params AISuggestedAction[] suggestions) : IAISuggestionProvider
    {
        public ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
            AISuggestionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AISuggestedAction>>(suggestions);
    }

    private sealed class ThrowingProvider : IAISuggestionProvider
    {
        public ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
            AISuggestionContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    private static AISuggestedAction Action(string type) =>
        new() { Type = type, Label = $"do {type}", DeepLink = $"/{type}" };

    private static AISuggestionResolver Resolver(IEnumerable<IAISuggestionProvider> providers) =>
        new(providers, NullLogger<AISuggestionResolver>.Instance);

    [Fact]
    public async Task No_providers_resolve_to_empty()
    {
        AISuggestionResolver resolver = Resolver([]);

        (await resolver.ResolveAsync(Context, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Aggregates_suggestions_from_all_providers()
    {
        AISuggestionResolver resolver = Resolver(
            [new StubProvider(Action("calendar.connect")), new StubProvider(Action("billing.setup"))]);

        IReadOnlyList<AISuggestedAction> result = await resolver.ResolveAsync(Context, TestContext.Current.CancellationToken);

        result.Select(a => a.Type).ShouldBe(["calendar.connect", "billing.setup"], ignoreOrder: true);
    }

    [Fact]
    public async Task Deduplicates_by_type_keeping_the_first()
    {
        var first = new AISuggestedAction { Type = "calendar.connect", Label = "first", DeepLink = "/a" };
        var second = new AISuggestedAction { Type = "calendar.connect", Label = "second", DeepLink = "/b" };
        AISuggestionResolver resolver = Resolver([new StubProvider(first), new StubProvider(second)]);

        IReadOnlyList<AISuggestedAction> result = await resolver.ResolveAsync(Context, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Label.ShouldBe("first");
    }

    [Fact]
    public async Task A_throwing_provider_is_skipped_and_others_survive()
    {
        AISuggestionResolver resolver = Resolver(
            [new ThrowingProvider(), new StubProvider(Action("calendar.connect"))]);

        IReadOnlyList<AISuggestedAction> result = await resolver.ResolveAsync(Context, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Type.ShouldBe("calendar.connect");
    }
}
