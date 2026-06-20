using Granit.AI.Chat.Extensions;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Suggestions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.AI.Chat.Tests;

/// <summary>
/// Guards the DI wiring of the suggestion seam, not just its declaration. The resolver fans out over
/// an injected <see cref="IEnumerable{T}"/> of <see cref="IAISuggestionProvider"/>; the builder must
/// keep registering each provider additively (<c>AddScoped</c>). A regression to a single-registration
/// <c>TryAdd</c> would silently keep only one provider — every other module's suggestions would vanish
/// with all behavioural tests still green.
/// </summary>
public sealed class AIChatSuggestionsRegistrationTests
{
    private sealed class StubProviderA : IAISuggestionProvider
    {
        public ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
            AISuggestionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AISuggestedAction>>([]);
    }

    private sealed class StubProviderB : IAISuggestionProvider
    {
        public ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
            AISuggestionContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AISuggestedAction>>([]);
    }

    [Fact]
    public void AddGranitChatSuggestions_RegistersResolverAndAllProviders()
    {
        var services = new ServiceCollection();

        services.AddGranitChatSuggestions(builder => builder.Add<StubProviderA>().Add<StubProviderB>());

        // The single resolver default must be wired...
        services.Count(d => d.ServiceType == typeof(IAISuggestionResolver) &&
                            d.ImplementationType == typeof(AISuggestionResolver)).ShouldBe(1);

        // ...and every contributed provider must register additively (collection-consumed).
        Type[] providers = [.. services
            .Where(d => d.ServiceType == typeof(IAISuggestionProvider))
            .Select(d => d.ImplementationType!)];
        providers.ShouldBe([typeof(StubProviderA), typeof(StubProviderB)], ignoreOrder: true);
    }
}
