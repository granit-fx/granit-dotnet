using System.Net;
using System.Net.Http.Json;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// End-to-end tests that exercise the **real** Granit.Authorization pipeline against
/// <c>/admin/roles</c>: <c>DynamicPermissionPolicyProvider</c> resolves the permission
/// name to a policy, <c>PermissionAuthorizationHandler</c> invokes
/// <c>PermissionChecker</c>, which walks the grant providers and ends up in
/// <c>EfCorePermissionGrantStore</c> against real PostgreSQL.
/// </summary>
/// <remarks>
/// Permission grants are seeded as user-scope rows
/// (<c>ProviderName = "U"</c>, <c>ProviderKey = user-sub</c>). Role-scope and
/// client-scope grant coverage is left for the follow-ups that introduce realm/client
/// role plumbing (#1098 – #1100).
/// </remarks>
public sealed class GranitRoleEndpointsPermissionTests
    : IClassFixture<RoleEndpointsPermissionTestApplication>, IAsyncLifetime
{
    private readonly RoleEndpointsPermissionTestApplication _app;

    public GranitRoleEndpointsPermissionTests(RoleEndpointsPermissionTestApplication app) =>
        _app = app;

    public ValueTask InitializeAsync() => new(_app.ResetAsync());

    public ValueTask DisposeAsync() => default;

    // ─────────────────────────────────────────────────────────────────────
    // Authentication
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        using HttpClient client = _app.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_NoGrant_Returns403()
    {
        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Roles.Read grant — GET allowed, POST denied
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WithReadGrant_Returns200()
    {
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WithReadGrantOnly_Returns403()
    {
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host, tenantId = (Guid?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Roles.Manage grant — POST / PUT allowed, DELETE denied
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_WithManageGrant_Returns201()
    {
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Manage,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host, tenantId = (Guid?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_WithManageGrantOnly_Returns403()
    {
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Manage,
            cancellationToken: TestContext.Current.CancellationToken);
        RoleMetadata target = await _app.SeedRoleMetadataAsync(
            "Disposable", MultiTenancySide.Both, tenantId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.DeleteAsync(
            $"/admin/roles/{target.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Roles.Delete grant — DELETE allowed
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithDeleteGrant_Returns204()
    {
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Delete,
            cancellationToken: TestContext.Current.CancellationToken);
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            cancellationToken: TestContext.Current.CancellationToken);
        RoleMetadata target = await _app.SeedRoleMetadataAsync(
            "Disposable", MultiTenancySide.Both, tenantId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.DeleteAsync(
            $"/admin/roles/{target.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Tenant-scope grants
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_TenantScopeGrant_InMatchingTenantContext_Returns200()
    {
        var tenantA = Guid.NewGuid();
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            tenantId: tenantA,
            cancellationToken: TestContext.Current.CancellationToken);

        using IDisposable _ = _app.CurrentTenant.Change(tenantA);
        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_TenantScopeGrant_FromOtherTenantContext_Returns403()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            tenantId: tenantA,
            cancellationToken: TestContext.Current.CancellationToken);

        using IDisposable _ = _app.CurrentTenant.Change(tenantB);
        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_TenantScopeGrant_FromHostContext_Returns403()
    {
        var tenantA = Guid.NewGuid();
        await _app.GrantToUserAsync(
            RoleEndpointsPermissionTestApplication.TestUserId,
            IdentityLocalPermissions.Roles.Read,
            tenantId: tenantA,
            cancellationToken: TestContext.Current.CancellationToken);

        // No CurrentTenant.Change — stays at host.
        using HttpClient client = _app.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
