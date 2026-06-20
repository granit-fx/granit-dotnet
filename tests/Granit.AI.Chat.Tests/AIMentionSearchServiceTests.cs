using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Mentions;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class AIMentionSearchServiceTests
{
    /// <summary>A resolver returning <paramref name="count"/> deterministic suggestions, ACL-filtered by query.</summary>
    private sealed class FakeResolver(string type, int count = 3) : IAIMentionResolver
    {
        public string Type => type;

        public ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIMentionContext?>(null);

        public ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
            string query, int limit, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AIMentionSuggestion> all =
            [
                .. Enumerable.Range(1, count).Select(i => new AIMentionSuggestion
                {
                    // Echo a deliberately wrong Type to prove the service overwrites it with the registry key.
                    Type = "wrong",
                    Id = $"{type}-{i}",
                    Label = $"{type} {i}",
                    Description = query.Length == 0 ? null : query,
                }),
            ];
            return ValueTask.FromResult<IReadOnlyList<AIMentionSuggestion>>([.. all.Take(limit)]);
        }
    }

    /// <summary>Authorizes every resolver except those whose Type is in the denied set.</summary>
    private sealed class DenyingAuthorizer(params string[] deniedTypes) : IAIMentionAuthorizer
    {
        private readonly HashSet<string> _denied = new(deniedTypes, StringComparer.OrdinalIgnoreCase);

        public ValueTask<bool> IsAuthorizedAsync(IAIMentionResolver resolver, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!_denied.Contains(resolver.Type));
    }

    private static AIMentionSearchService Build(params IAIMentionResolver[] resolvers) =>
        new(new AIMentionRegistry(resolvers), new AllowAllMentionAuthorizer());

    private static AIMentionSearchService Build(IAIMentionAuthorizer authorizer, params IAIMentionResolver[] resolvers) =>
        new(new AIMentionRegistry(resolvers), authorizer);

    [Fact]
    public async Task Search_by_type_targets_only_that_resolver()
    {
        AIMentionSearchService service = Build(new FakeResolver("user"), new FakeResolver("invoice"));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", "invoice", 10, TestContext.Current.CancellationToken);

        results.ShouldNotBeEmpty();
        results.ShouldAllBe(s => s.Type == "invoice");
    }

    [Fact]
    public async Task Search_overwrites_resolver_echoed_type_with_registry_key()
    {
        AIMentionSearchService service = Build(new FakeResolver("user"));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", "user", 10, TestContext.Current.CancellationToken);

        results.ShouldAllBe(s => s.Type == "user");
    }

    [Fact]
    public async Task Unknown_type_returns_empty()
    {
        AIMentionSearchService service = Build(new FakeResolver("user"));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", "ledger", 10, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cross_type_search_merges_every_resolver()
    {
        AIMentionSearchService service = Build(new FakeResolver("user", 2), new FakeResolver("invoice", 2));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", null, 10, TestContext.Current.CancellationToken);

        results.Select(s => s.Type).Distinct().ShouldBe(["user", "invoice"], ignoreOrder: true);
    }

    [Fact]
    public async Task Merged_results_are_capped_at_the_limit()
    {
        AIMentionSearchService service = Build(new FakeResolver("user", 5), new FakeResolver("invoice", 5));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", null, 3, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Unauthorized_type_is_skipped_in_a_targeted_search()
    {
        AIMentionSearchService service = Build(new DenyingAuthorizer("invoice"), new FakeResolver("invoice"));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", "invoice", 10, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unauthorized_types_are_filtered_out_of_a_cross_type_search()
    {
        AIMentionSearchService service = Build(
            new DenyingAuthorizer("invoice"), new FakeResolver("user", 2), new FakeResolver("invoice", 2));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", null, 10, TestContext.Current.CancellationToken);

        results.ShouldNotBeEmpty();
        results.ShouldAllBe(s => s.Type == "user");
    }

    [Fact]
    public async Task Non_positive_limit_returns_empty_without_querying()
    {
        AIMentionSearchService service = Build(new FakeResolver("user"));

        IReadOnlyList<AIMentionSuggestion> results =
            await service.SearchAsync("a", null, 0, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }
}
