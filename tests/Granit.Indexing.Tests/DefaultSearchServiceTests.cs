using Granit.Indexing.Diagnostics;
using Granit.Indexing.Exceptions;
using Granit.Indexing.Internal;
using Granit.Indexing.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Tests;

public sealed class DefaultSearchServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DefaultSearchService<Guid, string> Build(
        ISearchBackend<Guid, string> backend,
        ISearchResultAuthorizer<Guid> authorizer,
        GranitIndexingOptions? options = null,
        IEmptyResultRateLimiter? limiter = null)
    {
        TestMeterFactory meterFactory = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GranitIndexingOptions opts = options ?? new GranitIndexingOptions();
        IndexingMetrics metrics = new(meterFactory);
        ICurrentTenant tenant = NullTenantContext.Instance;
        return new DefaultSearchService<Guid, string>(
            backend,
            authorizer,
            limiter ?? new EmptyResultRateLimiter(time, opts, metrics, tenant),
            opts,
            metrics,
            tenant,
            time);
    }

    [Fact]
    public async Task Single_iteration_when_all_authorized()
    {
        // 60 backend rows. PageSize=20, multiplier=3 ⇒ fetch 60 on first call, all
        // authorized ⇒ exactly one backend hit.
        StubBackend backend = new(GenerateHits(60));
        AllowAll authorizer = new(multiplier: 3);

        SearchPage<string> page = await Build(backend, authorizer).SearchAsync(
            new SearchRequest("x", Page: 1, PageSize: 20), Ct);

        backend.Calls.Count.ShouldBe(1);
        backend.Calls[0].Offset.ShouldBe(0);
        backend.Calls[0].Limit.ShouldBe(60);
        page.Items.Count.ShouldBe(20);
        page.HitAuthorizationLimit.ShouldBeFalse();
        page.BackendHitCount.ShouldBe(60);
    }

    [Fact]
    public async Task Window_doubles_on_each_iteration_until_filled()
    {
        // multiplier=1 ⇒ window starts at 20. Then doubles: 40, 80.
        StubBackend backend = new(GenerateHits(200));
        EveryNth authorizer = new(every: 5, multiplier: 1);

        SearchPage<string> page = await Build(backend, authorizer).SearchAsync(
            new SearchRequest("x", Page: 1, PageSize: 20), Ct);

        backend.Calls[0].Limit.ShouldBe(20);
        backend.Calls[1].Limit.ShouldBe(40);
        backend.Calls[2].Limit.ShouldBe(80);

        backend.Calls[0].Offset.ShouldBe(0);
        backend.Calls[1].Offset.ShouldBe(20);
        backend.Calls[2].Offset.ShouldBe(60);

        page.Items.Count.ShouldBe(20);
        page.HitAuthorizationLimit.ShouldBeFalse();
    }

    [Fact]
    public async Task Stops_when_backend_exhausted_and_returns_partial_page()
    {
        StubBackend backend = new(GenerateHits(30));
        EveryNth authorizer = new(every: 2, multiplier: 1);

        SearchPage<string> page = await Build(backend, authorizer).SearchAsync(
            new SearchRequest("x", Page: 1, PageSize: 20), Ct);

        page.Items.Count.ShouldBe(15);
        page.HitAuthorizationLimit.ShouldBeFalse();
    }

    [Fact]
    public async Task Hits_authorization_depth_ceiling_and_flags_response()
    {
        StubBackend backend = new(GenerateHits(10_000));
        AuthorizeNone authorizer = new(multiplier: 1);
        var opts = new GranitIndexingOptions { MaxAuthorizationDepth = 500 };

        SearchPage<string> page = await Build(backend, authorizer, opts).SearchAsync(
            new SearchRequest("x", Page: 1, PageSize: 20), Ct);

        page.Items.ShouldBeEmpty();
        page.HitAuthorizationLimit.ShouldBeTrue();
        page.BackendHitCount.ShouldBe(500);
    }

    [Fact]
    public async Task Empty_result_throttle_throws_when_principal_exceeds_cap()
    {
        StubBackend backend = new(GenerateHits(0));
        AllowAll authorizer = new(multiplier: 1);
        var opts = new GranitIndexingOptions { MaxEmptyResultQueriesPerPrincipalPerMinute = 2 };
        DefaultSearchService<Guid, string> service = Build(backend, authorizer, opts);

        SearchRequest request = new("x", PrincipalIdentifier: "alice");

        await service.SearchAsync(request, Ct);
        await service.SearchAsync(request, Ct);

        EmptyResultRateLimitedException ex = await Should.ThrowAsync<EmptyResultRateLimitedException>(
            async () => await service.SearchAsync(request, Ct));

        // The exception carries the hash, never the raw principal.
        ex.PrincipalIdentifierHash.ShouldNotBe("alice");
        ex.PrincipalIdentifierHash.Length.ShouldBe(16);
        ex.Reason.ShouldBe("empty_result_rate_limited");
    }

    [Fact]
    public async Task Empty_result_throttle_is_skipped_when_principal_missing()
    {
        StubBackend backend = new(GenerateHits(0));
        AllowAll authorizer = new(multiplier: 1);
        var opts = new GranitIndexingOptions { MaxEmptyResultQueriesPerPrincipalPerMinute = 1 };
        DefaultSearchService<Guid, string> service = Build(backend, authorizer, opts);

        for (int i = 0; i < 10; i++)
        {
            await Should.NotThrowAsync(async () =>
                await service.SearchAsync(new SearchRequest("x"), Ct));
        }
    }

    [Fact]
    public async Task Multiplier_from_authorizer_is_applied_on_first_iteration()
    {
        StubBackend backend = new(GenerateHits(200));
        AllowAll authorizer = new(multiplier: 10);

        await Build(backend, authorizer).SearchAsync(
            new SearchRequest("x", Page: 1, PageSize: 20), Ct);

        backend.Calls[0].Limit.ShouldBe(200); // 20 * 10
    }

    private static SearchHit<Guid, string>[] GenerateHits(int count)
    {
        var hits = new SearchHit<Guid, string>[count];
        for (int i = 0; i < count; i++)
        {
            hits[i] = new SearchHit<Guid, string>(MakeKey(i), $"row-{i}", count - i);
        }
        return hits;
    }

    private static Guid MakeKey(int i)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, i);
        return new Guid(bytes);
    }

    private sealed class StubBackend(IReadOnlyList<SearchHit<Guid, string>> universe) : ISearchBackend<Guid, string>
    {
        public List<(int Offset, int Limit)> Calls { get; } = [];
        public string Name => "stub";

        public Task<BackendSearchPage<Guid, string>> SearchAsync(
            SearchRequest request, int offset, int limit, CancellationToken cancellationToken = default)
        {
            Calls.Add((offset, limit));
            int available = Math.Max(0, universe.Count - offset);
            int take = Math.Min(limit, available);
            var slice = new SearchHit<Guid, string>[take];
            for (int i = 0; i < take; i++)
            {
                slice[i] = universe[offset + i];
            }
            bool hasMore = offset + take < universe.Count;
            return Task.FromResult(new BackendSearchPage<Guid, string>(slice, hasMore));
        }
    }

    private sealed class AllowAll(int multiplier) : ISearchResultAuthorizer<Guid>
    {
        public int RecommendedInitialMultiplier => multiplier;
        public Task<AuthorizedResult<Guid>> FilterAsync(IReadOnlyList<Guid> candidates, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthorizedResult<Guid>(candidates));
    }

    private sealed class AuthorizeNone(int multiplier) : ISearchResultAuthorizer<Guid>
    {
        public int RecommendedInitialMultiplier => multiplier;
        public Task<AuthorizedResult<Guid>> FilterAsync(IReadOnlyList<Guid> candidates, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthorizedResult<Guid>([]));
    }

    private sealed class EveryNth(int every, int multiplier) : ISearchResultAuthorizer<Guid>
    {
        public int RecommendedInitialMultiplier => multiplier;
        public Task<AuthorizedResult<Guid>> FilterAsync(IReadOnlyList<Guid> candidates, CancellationToken cancellationToken = default)
        {
            List<Guid> allowed = [];
            byte[] bytes = new byte[16];
            foreach (Guid key in candidates)
            {
                key.TryWriteBytes(bytes);
                int index = BitConverter.ToInt32(bytes, 0);
                if (index % every == 0)
                {
                    allowed.Add(key);
                }
            }
            return Task.FromResult(new AuthorizedResult<Guid>(allowed));
        }
    }
}
