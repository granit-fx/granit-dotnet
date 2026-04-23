using System.Net;
using System.Net.Http.Json;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// HTTP-level tests that exercise the REAL permission-grant enforcement pipeline
/// (<c>DynamicPermissionPolicyProvider</c> → <c>PermissionAuthorizationHandler</c>
/// → <c>PermissionChecker</c>) against the <c>/admin/roles</c> endpoints.
/// </summary>
/// <remarks>
/// The companion <see cref="GranitRoleEndpointsTests"/> focuses on visibility /
/// validation logic with a permissive policy provider; this class proves the
/// authorization layer matches the route × permission contract declared on each
/// endpoint via <c>RequireAuthorization(...)</c> and that grant scoping
/// (<c>TenantId</c>) is honored.
/// </remarks>
public sealed class GranitRoleEndpointsPermissionTests
    : IClassFixture<RoleEndpointsPermissionTestApplication>, IAsyncLifetime
{
    private readonly RoleEndpointsPermissionTestApplication _app;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public GranitRoleEndpointsPermissionTests(RoleEndpointsPermissionTestApplication app) => _app = app;

    public ValueTask InitializeAsync() => new(_app.ResetAsync());

    public ValueTask DisposeAsync() => default;

    // ─────────────────────────────────────────────────────────────────────
    // Unauthenticated
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_Get_Returns401()
    {
        using HttpClient client = _app.HttpClient;

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unauthenticated_Post_Returns401()
    {
        using HttpClient client = _app.HttpClient;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Authenticated, no grant
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Authenticated_NoGrant_Get_Returns403()
    {
        HttpClient client = AuthenticatedClient(userId: "alice");

        HttpResponseMessage response = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authenticated_NoGrant_Post_Returns403()
    {
        HttpClient client = AuthenticatedClient(userId: "alice");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // IdentityLocal.Roles.Read — grants GET but not POST
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadGrant_Get_Returns200_Post_Returns403()
    {
        await _app.SeedUserGrantAsync("reader", IdentityLocalPermissions.Roles.Read);
        HttpClient client = AuthenticatedClient(userId: "reader");

        HttpResponseMessage get = await client.GetAsync(
            "/admin/roles", TestContext.Current.CancellationToken);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage post = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);
        post.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // IdentityLocal.Roles.Manage — grants POST/PUT but not DELETE
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ManageGrant_Post_Returns201_Delete_Returns403()
    {
        await _app.SeedUserGrantAsync("manager", IdentityLocalPermissions.Roles.Manage);
        HttpClient client = AuthenticatedClient(userId: "manager");

        HttpResponseMessage post = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);
        post.StatusCode.ShouldBe(HttpStatusCode.Created);

        RoleResponseLite? created = await post.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        HttpResponseMessage delete = await client.DeleteAsync(
            $"/admin/roles/{created.Id:D}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManageGrant_Put_Returns200()
    {
        await _app.SeedUserGrantAsync("manager", IdentityLocalPermissions.Roles.Manage);
        HttpClient client = AuthenticatedClient(userId: "manager");

        HttpResponseMessage post = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);
        post.StatusCode.ShouldBe(HttpStatusCode.Created);
        RoleResponseLite? created = await post.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);

        HttpResponseMessage put = await client.PutAsJsonAsync(
            $"/admin/roles/{created!.Id:D}",
            new { name = "SeniorAuditor" },
            TestContext.Current.CancellationToken);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ─────────────────────────────────────────────────────────────────────
    // IdentityLocal.Roles.Delete — grants DELETE
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteGrant_Delete_Returns204()
    {
        // Seed both Manage (to create a role first via the endpoint) and Delete.
        await _app.SeedUserGrantAsync("destroyer", IdentityLocalPermissions.Roles.Manage);
        await _app.SeedUserGrantAsync("destroyer", IdentityLocalPermissions.Roles.Delete);
        HttpClient client = AuthenticatedClient(userId: "destroyer");

        HttpResponseMessage post = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);
        RoleResponseLite? created = await post.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);

        HttpResponseMessage delete = await client.DeleteAsync(
            $"/admin/roles/{created!.Id:D}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteGrant_WithoutManage_CannotPostButCanDelete()
    {
        await _app.SeedUserGrantAsync("destroyer", IdentityLocalPermissions.Roles.Delete);
        HttpClient client = AuthenticatedClient(userId: "destroyer");

        // POST is refused with the Delete-only grant.
        HttpResponseMessage post = await client.PostAsJsonAsync(
            "/admin/roles",
            new { name = "Auditor", multiTenancySide = (int)MultiTenancySide.Host },
            TestContext.Current.CancellationToken);
        post.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Seed a role directly so we can prove DELETE works end-to-end.
        Guid seededId = await SeedHostRoleDirectlyAsync("Auditor");
        HttpResponseMessage delete = await client.DeleteAsync(
            $"/admin/roles/{seededId:D}", TestContext.Current.CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Tenant-scope grant isolation — TenantId on the grant gates the caller's context
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantScopedGrant_MatchingTenant_Returns200_OtherTenant_Returns403()
    {
        // Grant scoped to tenant A only.
        await _app.SeedUserGrantAsync("carol", IdentityLocalPermissions.Roles.Read, tenantId: _tenantA);
        HttpClient client = AuthenticatedClient(userId: "carol");

        // Inside tenant A → allowed.
        using (_app.CurrentTenant.Change(_tenantA))
        {
            HttpResponseMessage okInsideA = await client.GetAsync(
                "/admin/roles", TestContext.Current.CancellationToken);
            okInsideA.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Inside tenant B → denied (grant doesn't apply).
        using (_app.CurrentTenant.Change(_tenantB))
        {
            HttpResponseMessage forbiddenInsideB = await client.GetAsync(
                "/admin/roles", TestContext.Current.CancellationToken);
            forbiddenInsideB.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Host context (null tenant) → denied (tenant-scoped grant doesn't bubble up).
        using (_app.CurrentTenant.Change(id: null))
        {
            HttpResponseMessage forbiddenAtHost = await client.GetAsync(
                "/admin/roles", TestContext.Current.CancellationToken);
            forbiddenAtHost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private HttpClient AuthenticatedClient(string userId, params string[] roles)
    {
        HttpClient client = _app.HttpClient;
        client.DefaultRequestHeaders.Remove(PermissionTestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(PermissionTestAuthHandler.RolesHeader);
        client.DefaultRequestHeaders.Add(PermissionTestAuthHandler.UserIdHeader, userId);
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                PermissionTestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    private async Task<Guid> SeedHostRoleDirectlyAsync(string name)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        var metadata = RoleMetadata.Create(
            id: Guid.NewGuid(),
            name: name,
            multiTenancySide: MultiTenancySide.Host,
            tenantId: null,
            clientId: null,
            description: null,
            isSystem: false);

        await store.AddAsync(metadata, TestContext.Current.CancellationToken);
        return metadata.Id;
    }

    private sealed record RoleResponseLite(
        Guid Id,
        string Name,
        MultiTenancySide MultiTenancySide,
        Guid? TenantId,
        string? ClientId,
        string? Description,
        bool IsSystem);
}
