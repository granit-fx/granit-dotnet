using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.Mentions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Granit.Mentions.Tests;

public sealed class MentionLookupSourceTests
{
    private sealed class FakeResolver(string type, int count = 3, string? requiredPermission = null) : IMentionResolver
    {
        public string Type => type;
        public string? RequiredPermission => requiredPermission;

        public ValueTask<IReadOnlyList<MentionSuggestion>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MentionSuggestion>>(
            [
                .. Enumerable.Range(1, count).Take(limit).Select(i => new MentionSuggestion
                {
                    Type = type,
                    Id = $"{type}-{i}",
                    Label = $"{type} {i}",
                    Description = i == 1 ? "desc" : null,
                }),
            ]);

        public ValueTask<MentionTarget?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MentionTarget?>(id.EndsWith("-1", StringComparison.Ordinal)
                ? new MentionTarget { Type = type, Id = id, Label = $"{type} {id}", Content = $"content-of-{id}" }
                : null);
    }

    private readonly IPermissionChecker _checker = Substitute.For<IPermissionChecker>();

    private MentionLookupSource Build(params IMentionResolver[] resolvers) =>
        new(new MentionRegistry(resolvers), new PermissionMentionAuthorizer(_checker),
            NullLogger<MentionLookupSource>.Instance);

    private static LookupQuery Query(string? search = "a", int pageSize = 8, string? type = null) =>
        new(Search: search, PageSize: pageSize,
            Scope: type is null ? null : new Dictionary<string, string?> { ["type"] = type });

    [Fact]
    public void Name_is_mentions()
    {
        Build().Name.ShouldBe("mentions");
    }

    [Fact]
    public async Task Search_fans_out_across_all_types_and_stamps_composite_value()
    {
        MentionLookupSource source = Build(new FakeResolver("user", 1), new FakeResolver("invoice", 1));

        LookupResult result = await source.SearchAsync(Query(), TestContext.Current.CancellationToken);

        result.Items.Select(i => (string)i.Value).ShouldBe(["user:user-1", "invoice:invoice-1"], ignoreOrder: true);
        result.Items.ShouldContain(i => i.Extra!["type"]!.Equals("user") && i.Extra.ContainsKey("description"));
    }

    [Fact]
    public async Task Search_narrows_to_a_single_type_via_scope()
    {
        MentionLookupSource source = Build(new FakeResolver("user", 2), new FakeResolver("invoice", 2));

        LookupResult result = await source.SearchAsync(Query(type: "invoice"), TestContext.Current.CancellationToken);

        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(i => ((string)i.Value).StartsWith("invoice:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_skips_a_type_the_caller_is_not_authorized_for()
    {
        _checker.IsGrantedAsync("invoices.read", Arg.Any<CancellationToken>()).Returns(false);
        MentionLookupSource source = Build(
            new FakeResolver("user", 1), new FakeResolver("invoice", 1, requiredPermission: "invoices.read"));

        LookupResult result = await source.SearchAsync(Query(), TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(i => ((string)i.Value).StartsWith("user:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_caps_results_at_the_page_size()
    {
        MentionLookupSource source = Build(new FakeResolver("user", 5), new FakeResolver("invoice", 5));

        LookupResult result = await source.SearchAsync(Query(pageSize: 3), TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Resolve_decodes_composite_value_and_returns_content()
    {
        MentionLookupSource source = Build(new FakeResolver("user"));

        LookupItem? item = await source.ResolveByValueAsync("user:user-1", TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item.Label.ShouldBe("user user-1");
        item.Extra!["content"].ShouldBe("content-of-user-1");
    }

    [Fact]
    public async Task Resolve_returns_null_for_an_unknown_type()
    {
        MentionLookupSource source = Build(new FakeResolver("user"));

        (await source.ResolveByValueAsync("ledger:1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_returns_null_when_the_entity_is_absent()
    {
        MentionLookupSource source = Build(new FakeResolver("user"));

        (await source.ResolveByValueAsync("user:user-9", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_returns_null_when_unauthorized()
    {
        _checker.IsGrantedAsync("invoices.read", Arg.Any<CancellationToken>()).Returns(false);
        MentionLookupSource source = Build(new FakeResolver("invoice", requiredPermission: "invoices.read"));

        (await source.ResolveByValueAsync("invoice:invoice-1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_returns_null_for_a_malformed_value()
    {
        MentionLookupSource source = Build(new FakeResolver("user"));

        (await source.ResolveByValueAsync("nocolon", TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
