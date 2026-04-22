using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// End-to-end tests for <see cref="IGranitRoleOrchestrator"/> against real PostgreSQL:
/// proves the compensating-write strategy converges the "every GranitRole has matching
/// RoleMetadata" invariant even when the metadata write fails (and vice-versa for
/// rename). Also pins down the system-role protection rules.
/// </summary>
public sealed class RoleOrchestratorTests : IClassFixture<RoleOrchestratorTestApplication>, IAsyncLifetime
{
    private readonly RoleOrchestratorTestApplication _app;

    public RoleOrchestratorTests(RoleOrchestratorTestApplication app) => _app = app;

    public ValueTask InitializeAsync() => new(_app.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => default;

    // ─────────────────────────────────────────────────────────────────────
    // CreateAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_HappyPath_PersistsRoleAndMetadata()
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        RoleMetadata created = await orchestrator.CreateAsync(
            new CreateRoleCommand(
                Name: "Manager",
                MultiTenancySide: MultiTenancySide.Both,
                TenantId: null,
                Description: "Business manager."),
            TestContext.Current.CancellationToken);

        created.Name.ShouldBe("Manager");

        GranitRole? identityRole = await roleManager.FindByIdAsync(created.Id.ToString("D"));
        identityRole.ShouldNotBeNull();
        identityRole.Name.ShouldBe("Manager");
        identityRole.Description.ShouldBe("Business manager.");

        RoleMetadata? metadata = await store.FindByIdAsync(created.Id, TestContext.Current.CancellationToken);
        metadata.ShouldNotBeNull();
        metadata.MultiTenancySide.ShouldBe(MultiTenancySide.Both);
        metadata.TenantId.ShouldBeNull();
        metadata.IsSystem.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_DuplicateMetadataName_CompensatesByDeletingGranitRole()
    {
        // Seed a metadata-only row that will collide with the orchestrator's insert.
        // No matching GranitRole exists, so the Identity CreateAsync step succeeds;
        // the metadata AddAsync then violates the (Name, TenantId, ClientId) unique
        // index and must trigger the compensating delete.
        await SeedMetadataOnlyAsync("Manager", MultiTenancySide.Both, tenantId: null);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await orchestrator.CreateAsync(
                new CreateRoleCommand("Manager", MultiTenancySide.Both, TenantId: null),
                TestContext.Current.CancellationToken));

        // Invariant: no orphan GranitRole remains with display name "Manager".
        GranitRole? orphan = await roleManager.FindByNameAsync("Manager");
        orphan.ShouldBeNull();
    }

    // ─────────────────────────────────────────────────────────────────────
    // RenameAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenameAsync_HappyPath_UpdatesBothRoleAndMetadata()
    {
        RoleMetadata created = await CreateRoleViaOrchestratorAsync(
            "Alpha", MultiTenancySide.Both, tenantId: null);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        await orchestrator.RenameAsync(
            created.Id,
            newName: "AlphaPrime",
            newDescription: "Renamed.",
            TestContext.Current.CancellationToken);

        GranitRole? identityRole = await roleManager.FindByIdAsync(created.Id.ToString("D"));
        identityRole.ShouldNotBeNull();
        identityRole.Name.ShouldBe("AlphaPrime");
        identityRole.Description.ShouldBe("Renamed.");

        RoleMetadata? metadata = await store.FindByIdAsync(created.Id, TestContext.Current.CancellationToken);
        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("AlphaPrime");
        metadata.Description.ShouldBe("Renamed.");
    }

    [Fact]
    public async Task RenameAsync_MetadataNameCollision_RevertsIdentityRole()
    {
        // Alpha has a matched GranitRole + RoleMetadata (via orchestrator).
        RoleMetadata alpha = await CreateRoleViaOrchestratorAsync(
            "Alpha", MultiTenancySide.Both, tenantId: null);

        // Beta exists only in RoleMetadata. When we rename Alpha → "Beta", the
        // Identity update on Alpha succeeds (no "Beta" GranitRole yet) but the
        // metadata update hits the unique index → orchestrator compensates.
        await SeedMetadataOnlyAsync("Beta", MultiTenancySide.Both, tenantId: null);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await orchestrator.RenameAsync(
                alpha.Id,
                newName: "Beta",
                newDescription: null,
                TestContext.Current.CancellationToken));

        // Invariant: the Identity-side GranitRole kept its original name.
        GranitRole? identityRole = await roleManager.FindByIdAsync(alpha.Id.ToString("D"));
        identityRole.ShouldNotBeNull();
        identityRole.Name.ShouldBe("Alpha");
    }

    [Fact]
    public async Task RenameAsync_SystemRole_Refused()
    {
        // IsSystem=true is only settable via the factory — use the store directly so
        // we don't need an orchestrator code path for seeding system roles.
        RoleMetadata system = await SeedMetadataOnlyAsync(
            "SuperAdmin", MultiTenancySide.Host, tenantId: null, isSystem: true);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await orchestrator.RenameAsync(
                system.Id, newName: "NotSuperAdmin", newDescription: null,
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("system role");
    }

    // ─────────────────────────────────────────────────────────────────────
    // DeleteAsync
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_HappyPath_RemovesBothRoleAndMetadata()
    {
        RoleMetadata created = await CreateRoleViaOrchestratorAsync(
            "Disposable", MultiTenancySide.Both, tenantId: null);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        await orchestrator.DeleteAsync(created.Id, TestContext.Current.CancellationToken);

        GranitRole? identityRole = await roleManager.FindByIdAsync(created.Id.ToString("D"));
        identityRole.ShouldBeNull();

        RoleMetadata? metadata = await store.FindByIdAsync(created.Id, TestContext.Current.CancellationToken);
        metadata.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_SystemRole_Refused()
    {
        RoleMetadata system = await SeedMetadataOnlyAsync(
            "TenantAdministrator", MultiTenancySide.Both, tenantId: null, isSystem: true);

        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await orchestrator.DeleteAsync(system.Id, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("system role");
    }

    // ─────────────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<RoleMetadata> CreateRoleViaOrchestratorAsync(
        string name, MultiTenancySide side, Guid? tenantId)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        return await orchestrator.CreateAsync(
            new CreateRoleCommand(name, side, tenantId),
            TestContext.Current.CancellationToken);
    }

    private async Task<RoleMetadata> SeedMetadataOnlyAsync(
        string name, MultiTenancySide side, Guid? tenantId, bool isSystem = false)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        var metadata = RoleMetadata.Create(
            id: Guid.NewGuid(),
            name: name,
            multiTenancySide: side,
            tenantId: tenantId,
            clientId: null,
            description: null,
            isSystem: isSystem);

        hostCtx.Set<RoleMetadata>().Add(metadata);
        await hostCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return metadata;
    }
}
