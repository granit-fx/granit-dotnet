using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Extensions;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Granit.Mentions.Extensions;
using Granit.Mentions.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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

    /// <summary>A minimal registered lookup source, so the mention facade has a real sibling to fan out to.</summary>
    private sealed class StubLookupSource(string name) : ILookupSource
    {
        public string Name => name;
        public string? RequiredPermission => null;
        public IReadOnlyList<string> ScopeKeys => [];

        public ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new LookupResult([new LookupItem($"{name}-1", $"{name} 1")]));

        public ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken) =>
            ValueTask.FromResult<LookupItem?>(null);
    }

    /// <summary>
    /// Regression for the DI cycle (#2833). <c>LookupRegistry</c> consumes <see cref="IEnumerable{T}"/> of
    /// <see cref="ILookupSource"/> — which includes the mention facade — and the facade used to
    /// constructor-inject <see cref="ILookupRegistry"/>, closing a structural cycle. The container threw
    /// <c>"A circular dependency was detected for the service of type 'ILookupRegistry'"</c> at startup.
    /// <c>ValidateOnBuild</c> reproduces that detection; resolving the registry and facade proves the
    /// lazy <see cref="IServiceProvider"/> resolution broke the cycle without breaking the fan-out.
    /// </summary>
    [Fact]
    public void Container_validates_and_resolves_with_the_mention_facade_in_the_lookup_graph()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(Substitute.For<IPermissionChecker>());
        services.AddGranitDataLookup();
        services.AddScoped<ILookupSource>(_ => new StubLookupSource("user"));
        services.AddMentionSource("user");

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ILookupRegistry>().ShouldNotBeNull();
        scope.ServiceProvider.GetServices<ILookupSource>()
            .ShouldContain(source => source is MentionLookupSource);
    }
}
