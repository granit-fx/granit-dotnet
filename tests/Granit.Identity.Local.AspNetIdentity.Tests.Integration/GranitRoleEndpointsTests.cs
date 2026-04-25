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
/// Covers the visibility matrix applied by <c>ICurrentTenant</c>, tenant-scope
/// role happy paths, and the system-role protection rules. Permission-grant
/// enforcement is deliberately bypassed at the authorization-policy layer —
/// see <see cref="PermissiveAuthorizationPolicyProvider"/> for the rationale.
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
        await SeedMetadataAsync("SuperAdmin", MultiTenancySides.Host, tenantId: null);
        await SeedMetadataAsync("User", MultiTenancySides.Both, tenantId: null);
        await SeedMetadataAsync("Manager", MultiTenancySides.Tenant, tenantId: _tenantA);
        await SeedMetadataAsync("Manager", MultiTenancySides.Tenant, tenantId: _tenantB);

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
        await SeedMetadataAsync("SuperAdmin", MultiTenancySides.Host, tenantId: null);
        await SeedMetadataAsync("User", MultiTenancySides.Both, tenantId: null);
        await SeedMetadataAsync("Manager", MultiTenancySides.Tenant, tenantId: _tenantA);
        await SeedMetadataAsync("Manager", MultiTenancySides.Tenant, tenantId: _tenantB);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient
            .GetAsync("/admin/roles", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleResponseLite[]? roles = await response.Content
            .ReadFromJsonAsync<RoleResponseLite[]>(TestContext.Current.CancellationToken);

        roles.ShouldNotBeNull();
        roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)
            .ShouldBe(["Manager", "User"]);
        roles.ShouldNotContain(r => r.MultiTenancySides == MultiTenancySides.Host);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GetById — invisible roles surface as 404 (never 403, to prevent leak)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AsTenantAdmin_HostRole_Returns404()
    {
        RoleMetadata hostRole = await SeedMetadataAsync(
            "SuperAdmin", MultiTenancySides.Host, tenantId: null);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.GetAsync(
            $"/admin/roles/{hostRole.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_AsTenantAdmin_OtherTenantRole_Returns404()
    {
        RoleMetadata otherTenantRole = await SeedMetadataAsync(
            "Manager", MultiTenancySides.Tenant, tenantId: _tenantB);

        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.GetAsync(
            $"/admin/roles/{otherTenantRole.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST — Tenant-scope happy path (AllowTenantRoles = true default)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AsTenantAdmin_SideTenant_OwnTenant_Returns201()
    {
        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.PostAsJsonAsync(
            "/admin/roles",
            new
            {
                name = "Manager",
                multiTenancySides = (int)MultiTenancySides.Tenant,
                tenantId = _tenantA,
                description = "Tenant-A managers.",
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        RoleResponseLite? created = await response.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);

        created.ShouldNotBeNull();
        created.Name.ShouldBe("Manager");
        created.MultiTenancySides.ShouldBe(MultiTenancySides.Tenant);
        created.TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task Create_AsTenantAdmin_SideTenant_ForeignTenant_Returns403()
    {
        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        HttpResponseMessage response = await _app.HttpClient.PostAsJsonAsync(
            "/admin/roles",
            new
            {
                name = "Manager",
                multiTenancySides = (int)MultiTenancySides.Tenant,
                tenantId = _tenantB,
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsTenantAdmin_SideTenant_VisibleInOwnList_InvisibleToOtherTenant()
    {
        // Create a tenant-A role.
        using (_app.CurrentTenant.Change(_tenantA))
        {
            HttpResponseMessage create = await _app.HttpClient.PostAsJsonAsync(
                "/admin/roles",
                new
                {
                    name = "Manager",
                    multiTenancySides = (int)MultiTenancySides.Tenant,
                    tenantId = _tenantA,
                },
                TestContext.Current.CancellationToken);

            create.StatusCode.ShouldBe(HttpStatusCode.Created);

            // Tenant A sees it.
            HttpResponseMessage listA = await _app.HttpClient.GetAsync(
                "/admin/roles", TestContext.Current.CancellationToken);
            RoleResponseLite[]? rolesA = await listA.Content
                .ReadFromJsonAsync<RoleResponseLite[]>(TestContext.Current.CancellationToken);
            rolesA!.ShouldContain(r => r.Name == "Manager" && r.TenantId == _tenantA);
        }

        // Tenant B does not.
        using (_app.CurrentTenant.Change(_tenantB))
        {
            HttpResponseMessage listB = await _app.HttpClient.GetAsync(
                "/admin/roles", TestContext.Current.CancellationToken);
            RoleResponseLite[]? rolesB = await listB.Content
                .ReadFromJsonAsync<RoleResponseLite[]>(TestContext.Current.CancellationToken);
            rolesB!.ShouldNotContain(r => r.Name == "Manager" && r.TenantId == _tenantA);
        }
    }

    [Fact]
    public async Task Rename_AsTenantAdmin_OwnTenantRole_Returns200()
    {
        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        Guid roleId = await CreateViaEndpointAsync(
            "Manager", MultiTenancySides.Tenant, tenantId: _tenantA);

        HttpResponseMessage response = await _app.HttpClient.PutAsJsonAsync(
            $"/admin/roles/{roleId:D}",
            new { name = "SeniorManager", description = "Renamed" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        RoleResponseLite? updated = await response.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);
        updated!.Name.ShouldBe("SeniorManager");
        updated.Description.ShouldBe("Renamed");
    }

    [Fact]
    public async Task Delete_AsTenantAdmin_OwnTenantRole_Returns204()
    {
        using IDisposable _ = _app.CurrentTenant.Change(_tenantA);

        Guid roleId = await CreateViaEndpointAsync(
            "Manager", MultiTenancySides.Tenant, tenantId: _tenantA);

        HttpResponseMessage response = await _app.HttpClient.DeleteAsync(
            $"/admin/roles/{roleId:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getAfter = await _app.HttpClient.GetAsync(
            $"/admin/roles/{roleId:D}", TestContext.Current.CancellationToken);
        getAfter.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────
    // System role protection
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SystemRole_Returns403()
    {
        RoleMetadata system = await SeedMetadataAsync(
            "SuperAdmin", MultiTenancySides.Host, tenantId: null, isSystem: true);

        HttpResponseMessage response = await _app.HttpClient.DeleteAsync(
            $"/admin/roles/{system.Id:D}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rename_SystemRole_Returns403()
    {
        RoleMetadata system = await SeedMetadataAsync(
            "User", MultiTenancySides.Both, tenantId: null, isSystem: true);

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
            "Manager", MultiTenancySides.Tenant, tenantId: _tenantB);

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
                multiTenancySides = (int)MultiTenancySides.Host,
                tenantId = (Guid?)null,
                description = "Read-only platform auditor.",
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        RoleResponseLite? created = await response.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);

        created.ShouldNotBeNull();
        created.Name.ShouldBe("Auditor");
        created.MultiTenancySides.ShouldBe(MultiTenancySides.Host);
        created.IsSystem.ShouldBeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateViaEndpointAsync(
        string name, MultiTenancySides side, Guid? tenantId)
    {
        HttpResponseMessage response = await _app.HttpClient.PostAsJsonAsync(
            "/admin/roles",
            new
            {
                name,
                multiTenancySides = (int)side,
                tenantId,
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        RoleResponseLite? created = await response.Content
            .ReadFromJsonAsync<RoleResponseLite>(TestContext.Current.CancellationToken);
        return created!.Id;
    }

    private async Task<RoleMetadata> SeedMetadataAsync(
        string name, MultiTenancySides side, Guid? tenantId, bool isSystem = false)
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
        MultiTenancySides MultiTenancySides,
        Guid? TenantId,
        string? ClientId,
        string? Description,
        bool IsSystem);
}
