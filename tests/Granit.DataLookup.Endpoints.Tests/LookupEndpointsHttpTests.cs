using System.Net;
using System.Net.Http.Json;
using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Endpoints.Dtos;
using Granit.DataLookup.Endpoints.Extensions;
using Granit.DataLookup.Endpoints.Permissions;
using Granit.DataLookup.Extensions;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Granit.MultiTenancy;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Endpoints.Tests;

public sealed class LookupEndpointsHttpTests : IAsyncDisposable
{
    private const string Prefix = "/lookups";

    private readonly FakeLookupSource _colors = new("colors", LookupKind.Enum,
        items: [new LookupItem("red", "Red"), new LookupItem("green", "Green")]);
    private readonly FakeLookupSource _scoped = new("meter-definitions", LookupKind.QueryEngine,
        items: [new LookupItem(Guid.Empty, "Main meter")], scopeKeys: ["tenantId"]);
    private readonly FakeLookupSource _secured = new("tenants", LookupKind.QueryEngine,
        items: [new LookupItem(Guid.Empty, "Acme")], requiredPermission: "Platform.Tenants.Read");

    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly GranitEndpointTestHost _host;
    private readonly HttpClient _client;
    private readonly HttpClient _anon;

    public LookupEndpointsHttpTests()
    {
        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(DataLookupPermissions.Lookups.Read, p => p.RequireAuthenticatedUser());

                services.AddMetrics();
                services.AddGranitDataLookup();
                services.AddSingleton<ILookupSource>(_colors);
                services.AddSingleton<ILookupSource>(_scoped);
                services.AddSingleton<ILookupSource>(_secured);
                services.AddSingleton(_permissionChecker);
                services.AddSingleton(Substitute.For<ICurrentTenant>());
            },
            configureEndpoints: app => app.MapGranitDataLookups())
            .GetAwaiter().GetResult();

        _client = _host.CreateAuthenticatedClient();
        _anon = _host.CreateAnonymousClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _anon.Dispose();
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Manifest_lists_sources_with_kind_permission_and_scope()
    {
        LookupManifestResponse? body = await _client.GetFromJsonAsync<LookupManifestResponse>(
            Prefix, TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Lookups.Select(l => l.Name).ShouldBe(["colors", "meter-definitions", "tenants"]);

        LookupManifestEntryResponse colors = body.Lookups.First(l => l.Name == "colors");
        colors.Kind.ShouldBe(LookupKind.Enum);

        LookupManifestEntryResponse scoped = body.Lookups.First(l => l.Name == "meter-definitions");
        scoped.ScopeKeys.ShouldBe(["tenantId"]);

        body.Lookups.First(l => l.Name == "tenants").RequiredPermission.ShouldBe("Platform.Tenants.Read");
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        HttpResponseMessage response = await _anon.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_unknown_source_returns_404()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_returns_items_and_forwards_query()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/colors?search=re&page=2&pageSize=5", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        LookupResultResponse? body = await response.Content
            .ReadFromJsonAsync<LookupResultResponse>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Items.Select(i => i.Label).ShouldBe(["Red", "Green"]);
        body.TotalCount.ShouldBe(2);

        _colors.LastQuery.ShouldNotBeNull();
        _colors.LastQuery!.Search.ShouldBe("re");
        _colors.LastQuery.Page.ShouldBe(2);
        _colors.LastQuery.PageSize.ShouldBe(5);
    }

    [Fact]
    public async Task Search_round_trips_continuation_token()
    {
        _colors.NextContinuationToken = "cursor-2";

        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/colors?continuationToken=cursor-1", TestContext.Current.CancellationToken);

        LookupResultResponse? body = await response.Content
            .ReadFromJsonAsync<LookupResultResponse>(TestContext.Current.CancellationToken);

        _colors.LastQuery!.ContinuationToken.ShouldBe("cursor-1");
        body!.ContinuationToken.ShouldBe("cursor-2");
    }

    [Fact]
    public async Task Search_missing_required_scope_key_returns_400()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/meter-definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("tenantId");
    }

    [Fact]
    public async Task Search_with_scope_satisfied_returns_200_and_forwards_scope()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/meter-definitions?scope.tenantId=acme", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _scoped.LastQuery!.Scope.ShouldNotBeNull();
        _scoped.LastQuery.Scope!["tenantId"].ShouldBe("acme");
    }

    [Fact]
    public async Task Search_forbidden_when_permission_denied_returns_403()
    {
        _permissionChecker.IsGrantedAsync("Platform.Tenants.Read", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/tenants", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_allowed_when_permission_granted_returns_200()
    {
        _permissionChecker.IsGrantedAsync("Platform.Tenants.Read", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/tenants", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resolve_returns_item()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/colors/resolve?value=red", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        LookupItemResponse? body = await response.Content
            .ReadFromJsonAsync<LookupItemResponse>(TestContext.Current.CancellationToken);

        body!.Label.ShouldBe("Red");
    }

    [Fact]
    public async Task Resolve_missing_value_returns_400()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/colors/resolve", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resolve_unknown_value_returns_404()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"{Prefix}/colors/resolve?value=purple", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Configurable in-memory lookup source: records the last query it received and returns
    /// canned items, so the endpoint contract (routing, scope/permission gating, query
    /// forwarding, response shape) can be asserted without a database or localizer.
    /// </summary>
    private sealed class FakeLookupSource(
        string name,
        LookupKind kind,
        IReadOnlyList<LookupItem> items,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null) : ILookupSource, IKindProviderLookupSource
    {
        private readonly IReadOnlyList<LookupItem> _items = items;

        public string Name => name;
        public string? RequiredPermission => requiredPermission;
        public IReadOnlyList<string> ScopeKeys => scopeKeys ?? [];
        LookupKind IKindProviderLookupSource.Kind => kind;

        public LookupQuery? LastQuery { get; private set; }
        public string? NextContinuationToken { get; set; }

        public ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return ValueTask.FromResult(new LookupResult(_items, _items.Count, NextContinuationToken));
        }

        public ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
        {
            LookupItem? match = _items.FirstOrDefault(i => string.Equals(i.Value.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase));
            return ValueTask.FromResult(match);
        }
    }
}
