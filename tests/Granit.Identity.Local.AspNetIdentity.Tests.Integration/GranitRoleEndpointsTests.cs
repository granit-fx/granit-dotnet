using System.Net;
using System.Net.Http.Json;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// HTTP-level integration tests for the <c>/admin/roles</c> CRUD endpoints.
/// Covers the visibility matrix applied by <c>ICurrentTenant</c>, the
/// <c>AllowTenantRoles</c> Phase-1 refusal, and the system-role protection
/// rules. Permission-grant enforcement is deliberately bypassed at the
/// authorization-policy layer — see <see cref="PermissiveAuthorizationPolicyProvider"/>
/// for the rationale.
/// </summary>
public sealed class GranitRoleEndpointsTests
    : IClassFixture<RoleEndpointsTestApplication>, IAsyncLifetime
{
    private readonly RoleEndpointsTestApplication _app;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public GranitRoleEndpointsTests(RoleEndpointsTestApplication app) => _app = app;

    public ValueTask InitializeAsync() => new(_app.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => default;

    // ─────────────────────────────────────────────────────────────────────
    // Listing — visibility matrix
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_AsHostAdmin_SeesAllRoles()
    {
        await SeedMetadataAsync("SuperAdmin", MultiTenancySide.Host, tenantId: null);
        await SeedMetadataAsync("User", MultiTenancySide.Both, tenantId: null);
        await SeedMetadataAsync("Manager", MultiTenancySide.Tenant, tenantId: _tenantA);
        await SeedMetadataAsync("Manager", MultiTenancySide.Tenant, tenantId: _tenantB);

        HttpResponseMessage response = await _app.HttpClient
            .GetAsync("/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleResponseLite[]? roles = await response.Content
            .ReadFromJsonAsync<RoleResponseLite[]>(TestContext.Current.CancellationToken);

        roles.ShouldNotBeNull();
        roles.Length.ShouldBe(4);
    }

    [Fact]
    public async Task List_AsTenantAdmin_SeesBothAndOwnTenantOnly()
    {
        await SeedMetadataAsync("SuperAdmin", MultiTenancySide.Host, tenantId: null);
        await SeedMetadataAsync("User", MultiTenancySide.Both, tenantId: null);
        await SeedMetadataAsync("Manager", MultiTenancySide.Tenant, tenantId: _tenantA);
        await SeedMetadataAsync("Manager", MultiTenancySide.Tenant, tenantId: _tenantB);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient
            .GetAsync("/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleResponseLite[]? roles = await response.Content
            .ReadFromJsonAsync<RoleResponseLite[]>(TestContext.Current.CancellationToken);

        roles.ShouldNotBeNull();
        roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)
            .ShouldBe(["Manager", "User"]);
        roles.ShouldNotContain(r => r.MultiTenancySide == MultiTenancySide.Host);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GetById — invisible roles surface as 404 (never 403, to prevent leak)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AsTenantAdmin_HostRole_Returns404()
    {
        RoleMetadata hostRole = await SeedMetadataAsync(
            "SuperAdmin", MultiTenancySide.Host, tenantId: null);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.GetAsync(
            $"/admin/roles/{hostRole.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_AsTenantAdmin_OtherTenantRole_Returns404()
    {
        RoleMetadata otherTenantRole = await SeedMetadataAsync(
            "Manager", MultiTenancySide.Tenant, tenantId: _tenantB);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.GetAsync(
            $"/admin/roles/{otherTenantRole.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST — AllowTenantRoles flag (Phase 1 refusal)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_SideTenant_AllowTenantRolesFalse_Returns403()
    {
        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.PostAsJsonAsync(
            "/admin/roles",
            new
            {
                name = "Manager",
                multiTenancySide = (int)MultiTenancySide.Tenant,
                tenantId = _tenantA,
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // System role protection
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SystemRole_Returns403()
    {
        RoleMetadata system = await SeedMetadataAsync(
            "SuperAdmin", MultiTenancySide.Host, tenantId: null, isSystem: true);

        HttpResponseMessage response = await _app.HttpClient.DeleteAsync(
            $"/admin/roles/{system.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rename_SystemRole_Returns403()
    {
        RoleMetadata system = await SeedMetadataAsync(
            "User", MultiTenancySide.Both, tenantId: null, isSystem: true);

        HttpResponseMessage response = await _app.HttpClient.PutAsJsonAsync(
            $"/admin/roles/{system.Id:D}",
            new { name = "RenamedUser" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cross-tenant mutation — invisibility wins over 403 (no existence leak)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_AsTenantAdmin_OtherTenantRole_Returns404()
    {
        RoleMetadata otherTenantRole = await SeedMetadataAsync(
            "Manager", MultiTenancySide.Tenant, tenantId: _tenantB);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.PutAsJsonAsync(
            $"/admin/roles/{otherTenantRole.Id:D}",
            new { name = "Hijacked" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Happy path — proves the orchestrator is wired end-to-end through HTTP
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AsHostAdmin_SideHost_Returns201()
    {
        HttpResponseMessage response = await _app.HttpClient.PostAsJsonAsync(
            "/admin/roles",
            new
            {
                name = "Auditor",
                multiTenancySide = (int)MultiTenancySide.Host,
                tenantId = (Guid?)null,
                description = "Read-only platform auditor.",
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        RoleResponseLite? created = await response.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);

        created.ShouldNotBeNull();
        created.Name.ShouldBe("Auditor");
        created.MultiTenancySide.ShouldBe(MultiTenancySide.Host);
        created.IsSystem.ShouldBeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<RoleMetadata> SeedMetadataAsync(
        string name, MultiTenancySide side, Guid? tenantId, bool isSystem = false)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        var metadata = RoleMetadata.Create(
            id: Guid.NewGuid(),
            name: name,
            multiTenancySide: side,
            tenantId: tenantId,
            clientId: null,
            description: null,
            isSystem: isSystem);

        await store.AddAsync(metadata, TestContext.Current.CancellationToken);
        return metadata;
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
