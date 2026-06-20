using Granit.DataLookup.Sources;
using Granit.Mentions.Extensions;
using Granit.Mentions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.Mentions.Tests;

/// <summary>
/// Guards the DI wiring of the mention facade, not just its declaration. <c>LookupRegistry</c> builds
/// its name→source map from the injected <see cref="IEnumerable{T}"/> of <see cref="ILookupSource"/>;
/// the <c>mentions</c> facade only resolves if <see cref="MentionLookupSource"/> is actually in that
/// collection. A regression of the <c>TryAddEnumerable</c> registration to a single <c>TryAddScoped</c>
/// (or dropping it) would make the picker silently resolve nothing — or, registered twice via a plain
/// <c>AddScoped</c>, would crash the registry on a duplicate source name.
/// </summary>
public sealed class MentionsRegistrationTests
{
    private static int MentionFacadeCount(IServiceCollection services) =>
        services.Count(d =>
            d.ServiceType == typeof(ILookupSource) &&
            d.ImplementationType == typeof(MentionLookupSource));

    [Fact]
    public void AddGranitMentions_RegistersMentionFacadeAsLookupSource()
    {
        var services = new ServiceCollection();

        services.AddGranitMentions();

        MentionFacadeCount(services).ShouldBe(1);
        MentionLookup.SourceName.ShouldBe("mentions");
    }

    [Fact]
    public void AddGranitMentions_IsIdempotent()
    {
        var services = new ServiceCollection();

        services.AddGranitMentions();
        services.AddGranitMentions();

        // A non-idempotent registration would register MentionLookupSource twice; LookupRegistry
        // throws on a duplicate source name, so exactly one descriptor proves TryAddEnumerable holds.
        MentionFacadeCount(services).ShouldBe(1);
    }
}
