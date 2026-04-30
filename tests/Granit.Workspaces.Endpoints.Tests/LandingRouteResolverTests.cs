using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Workspaces.Endpoints.Landing;
using Granit.Workspaces.Endpoints.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Workspaces.Endpoints.Tests;

public sealed class LandingRouteResolverTests
{
    private const string FrameworkRoute = "/w/Granit.Framework";

    [Fact]
    public async Task Resolve_returns_framework_when_no_higher_tier_resolves()
    {
        LandingRouteResolver resolver = Build();

        LandingRouteResult result = await resolver.ResolveAsync(
            BuildUser("user-1"), TestContext.Current.CancellationToken);

        result.Route.ShouldBe(FrameworkRoute);
        result.Source.ShouldBe(LandingRouteSource.Framework);
    }

    [Fact]
    public async Task Resolve_prefers_personal_sticky_over_every_other_tier()
    {
        ILandingRouteStore store = Substitute.For<ILandingRouteStore>();
        store.GetStickyAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("/w/Sales");
        store.GetPinnedAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("/w/Pinned");

        IRoleLandingRouteProvider role = Substitute.For<IRoleLandingRouteProvider>();
        role.GetRoleDefaultAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns("/w/Role");

        LandingRouteResolver resolver = Build(store: store, role: role);

        LandingRouteResult result = await resolver.ResolveAsync(
            BuildUser("user-1"), TestContext.Current.CancellationToken);

        result.Route.ShouldBe("/w/Sales");
        result.Source.ShouldBe(LandingRouteSource.PersonalSticky);
    }

    [Fact]
    public async Task Resolve_falls_back_to_pinned_when_sticky_is_blocked_by_access_guard()
    {
        ILandingRouteStore store = Substitute.For<ILandingRouteStore>();
        store.GetStickyAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("/w/SecretWorkspace");
        store.GetPinnedAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("/w/Pinned");

        ILandingRouteAccessGuard guard = Substitute.For<ILandingRouteAccessGuard>();
        guard.CanAccessAsync("/w/SecretWorkspace", Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(false);
        guard.CanAccessAsync("/w/Pinned", Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(true);

        LandingRouteResolver resolver = Build(store: store, guard: guard);

        LandingRouteResult result = await resolver.ResolveAsync(
            BuildUser("user-1"), TestContext.Current.CancellationToken);

        result.Source.ShouldBe(LandingRouteSource.PersonalPinned);
    }

    [Fact]
    public async Task Resolve_skips_routes_that_fail_the_url_whitelist()
    {
        ILandingRouteStore store = Substitute.For<ILandingRouteStore>();
        store.GetPinnedAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("https://evil.example/w/Sales"); // not on the whitelist

        IRoleLandingRouteProvider role = Substitute.For<IRoleLandingRouteProvider>();
        role.GetRoleDefaultAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns("/w/Sales");

        LandingRouteResolver resolver = Build(store: store, role: role);

        LandingRouteResult result = await resolver.ResolveAsync(
            BuildUser("user-1"), TestContext.Current.CancellationToken);

        result.Source.ShouldBe(LandingRouteSource.Role);
    }

    [Fact]
    public async Task Resolve_uses_role_then_tenant_when_no_personal_preference_exists()
    {
        ITenantLandingRouteProvider tenant = Substitute.For<ITenantLandingRouteProvider>();
        tenant.GetTenantDefaultAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns("/w/Tenant");

        LandingRouteResolver resolver = Build(tenant: tenant);

        LandingRouteResult result = await resolver.ResolveAsync(
            BuildUser("user-1"), TestContext.Current.CancellationToken);

        result.Source.ShouldBe(LandingRouteSource.Tenant);
        result.Route.ShouldBe("/w/Tenant");
    }

    [Fact]
    public void IsAllowedRoute_rejects_routes_that_dont_match_a_prefix()
    {
        LandingRouteResolver resolver = Build();

        resolver.IsAllowedRoute("/w/Sales").ShouldBeTrue();
        resolver.IsAllowedRoute("/e/Granit.Parties.Party").ShouldBeTrue();
        resolver.IsAllowedRoute("https://elsewhere/w/Sales").ShouldBeFalse();
        resolver.IsAllowedRoute("/admin").ShouldBeFalse();
        resolver.IsAllowedRoute(null).ShouldBeFalse();
        resolver.IsAllowedRoute("").ShouldBeFalse();
    }

    private static LandingRouteResolver Build(
        ILandingRouteStore? store = null,
        IRoleLandingRouteProvider? role = null,
        ITenantLandingRouteProvider? tenant = null,
        ILandingRouteAccessGuard? guard = null)
    {
        WorkspacesEndpointsOptions options = new()
        {
            FrameworkLandingRoute = FrameworkRoute,
            LandingRouteAllowedPrefixes = ["/w/", "/e/"],
        };

        return new LandingRouteResolver(
            store ?? new EmptyStore(),
            role ?? new NullRole(),
            tenant ?? new NullTenant(),
            guard ?? new AcceptAll(),
            MsOptions.Create(options));
    }

    private static ClaimsPrincipal BuildUser(string sub) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, sub)], "test"));

    private sealed class EmptyStore : ILandingRouteStore
    {
        public Task<string?> GetStickyAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task<string?> GetPinnedAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task SetPinnedAsync(string userId, Guid? tenantId, string? route, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullRole : IRoleLandingRouteProvider
    {
        public Task<string?> GetRoleDefaultAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullTenant : ITenantLandingRouteProvider
    {
        public Task<string?> GetTenantDefaultAsync(Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class AcceptAll : ILandingRouteAccessGuard
    {
        public Task<bool> CanAccessAsync(string route, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
