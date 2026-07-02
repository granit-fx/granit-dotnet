using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Granit.Mentions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Granit.Mentions.Tests;

public sealed class MentionLookupSourceTests
{
    /// <summary>A lookup source returning <paramref name="count"/> deterministic items.</summary>
    private sealed class FakeLookupSource(string name, int count = 3, string? requiredPermission = null) : ILookupSource
    {
        public string Name => name;
        public string? RequiredPermission => requiredPermission;
        public IReadOnlyList<string> ScopeKeys => [];

        public ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new LookupResult(
            [
                .. Enumerable.Range(1, count).Take(query.PageSize).Select(i =>
                    new LookupItem($"{name}-{i}", $"{name} {i}",
                        new Dictionary<string, object?> { ["email"] = $"{name}{i}@x.io" })),
            ]));

        public ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken) =>
            ValueTask.FromResult<LookupItem?>(value.ToString() == $"{name}-1"
                ? new LookupItem($"{name}-1", $"{name} 1", new Dictionary<string, object?> { ["email"] = $"{name}1@x.io" })
                : null);
    }

    private sealed class FakeLookupRegistry(params ILookupSource[] sources) : ILookupRegistry
    {
        private readonly Dictionary<string, ILookupSource> _byName =
            sources.ToDictionary(s => s.Name, StringComparer.Ordinal);

        public ILookupSource? Resolve(string name) => _byName.GetValueOrDefault(name);

        public IReadOnlyList<LookupManifestEntry> GetManifest() => [];
    }

    private readonly IPermissionChecker _checker = Substitute.For<IPermissionChecker>();

    private MentionLookupSource Build(ILookupSource[] sources, params string[] mentionable)
    {
        // MentionLookupSource resolves ILookupRegistry lazily off IServiceProvider (breaks the DI cycle);
        // hand it a provider carrying the fake registry.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ILookupRegistry>(new FakeLookupRegistry(sources))
            .BuildServiceProvider();
        return new([.. mentionable.Select(n => new MentionSource(n))], provider, _checker,
            NullLogger<MentionLookupSource>.Instance);
    }

    private static LookupQuery Query(string? search = "a", int pageSize = 8, string? type = null) =>
        new(Search: search, PageSize: pageSize,
            Scope: type is null ? null : new Dictionary<string, string?> { ["type"] = type });

    [Fact]
    public void Name_is_mentions() =>
        Build([new FakeLookupSource("user")], "user").Name.ShouldBe("mentions");

    [Fact]
    public async Task Search_fans_out_across_tagged_sources_and_stamps_composite_value()
    {
        MentionLookupSource source = Build(
            [new FakeLookupSource("user", 1), new FakeLookupSource("invoice", 1)], "user", "invoice");

        LookupResult result = await source.SearchAsync(Query(), TestContext.Current.CancellationToken);

        result.Items.Select(i => (string)i.Value).ShouldBe(["user:user-1", "invoice:invoice-1"], ignoreOrder: true);
        result.Items.ShouldAllBe(i => i.Extra!["type"] != null && i.Extra.ContainsKey("email"));
    }

    [Fact]
    public async Task Search_ignores_untagged_sources()
    {
        MentionLookupSource source = Build(
            [new FakeLookupSource("user", 1), new FakeLookupSource("secret", 1)], "user");

        LookupResult result = await source.SearchAsync(Query(), TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(i => ((string)i.Value).StartsWith("user:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_narrows_to_a_single_type_via_scope()
    {
        MentionLookupSource source = Build(
            [new FakeLookupSource("user", 2), new FakeLookupSource("invoice", 2)], "user", "invoice");

        LookupResult result = await source.SearchAsync(Query(type: "invoice"), TestContext.Current.CancellationToken);

        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(i => ((string)i.Value).StartsWith("invoice:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_skips_a_type_the_caller_is_not_authorized_for()
    {
        _checker.IsGrantedAsync("invoices.read", Arg.Any<CancellationToken>()).Returns(false);
        MentionLookupSource source = Build(
            [new FakeLookupSource("user", 1), new FakeLookupSource("invoice", 1, "invoices.read")], "user", "invoice");

        LookupResult result = await source.SearchAsync(Query(), TestContext.Current.CancellationToken);

        result.Items.ShouldAllBe(i => ((string)i.Value).StartsWith("user:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_caps_results_at_the_page_size()
    {
        MentionLookupSource source = Build(
            [new FakeLookupSource("user", 5), new FakeLookupSource("invoice", 5)], "user", "invoice");

        LookupResult result = await source.SearchAsync(Query(pageSize: 3), TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Resolve_decodes_composite_and_restamps()
    {
        MentionLookupSource source = Build([new FakeLookupSource("user")], "user");

        LookupItem? item = await source.ResolveByValueAsync("user:user-1", TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item.Value.ShouldBe("user:user-1");
        item.Label.ShouldBe("user 1");
        item.Extra!["type"].ShouldBe("user");
    }

    [Fact]
    public async Task Resolve_returns_null_for_an_untagged_type()
    {
        MentionLookupSource source = Build([new FakeLookupSource("secret")], "user");

        (await source.ResolveByValueAsync("secret:secret-1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_returns_null_when_unauthorized()
    {
        _checker.IsGrantedAsync("invoices.read", Arg.Any<CancellationToken>()).Returns(false);
        MentionLookupSource source = Build([new FakeLookupSource("invoice", requiredPermission: "invoices.read")], "invoice");

        (await source.ResolveByValueAsync("invoice:invoice-1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_returns_null_for_a_malformed_value()
    {
        MentionLookupSource source = Build([new FakeLookupSource("user")], "user");

        (await source.ResolveByValueAsync("nocolon", TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
