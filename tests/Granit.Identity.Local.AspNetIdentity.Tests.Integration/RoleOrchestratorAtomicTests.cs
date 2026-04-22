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
/// End-to-end tests for <see cref="GranitRoleOrchestrator"/>'s <b>atomic</b> execution
/// path — the shared-connection EF Core transaction that commits Identity + metadata
/// in a single Postgres transaction. Complements <see cref="RoleOrchestratorTests"/>
/// which exercises the compensating-write fallback.
/// </summary>
public sealed class RoleOrchestratorAtomicTests
    : IClassFixture<RoleOrchestratorAtomicTestApplication>, IAsyncLifetime
{
    private readonly RoleOrchestratorAtomicTestApplication _app;

    public RoleOrchestratorAtomicTests(RoleOrchestratorAtomicTestApplication app) => _app = app;

    public ValueTask InitializeAsync() => new(_app.ResetDatabaseAsync());

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task CreateAsync_HappyPath_PersistsBothRowsInSingleTransaction()
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        RoleMetadata created = await orchestrator.CreateAsync(
            new CreateRoleCommand(
                Name: "AtomicManager",
                MultiTenancySide: MultiTenancySide.Both,
                TenantId: null,
                Description: "Verifies atomic commit."),
            TestContext.Current.CancellationToken);

        created.Name.ShouldBe("AtomicManager");

        GranitRole? identityRole = await roleManager.FindByIdAsync(created.Id.ToString("D"));
        identityRole.ShouldNotBeNull();
        identityRole.Name.ShouldBe("AtomicManager");

        RoleMetadata? metadata = await store.FindByIdAsync(created.Id, TestContext.Current.CancellationToken);
        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("AtomicManager");
    }

    [Fact]
    public async Task CreateAsync_MetadataUniqueViolation_RollsBackWithoutCompensation()
    {
        // Seed a metadata-only row (via direct factory context, not orchestrator) so the
        // orchestrator's metadata INSERT later hits the (Name, TenantId, ClientId) unique
        // index and the whole transaction rolls back atomically.
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IDbContextFactory<TestHostDbContext> hostFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<TestHostDbContext>>();
        await using (TestHostDbContext hostCtx = await hostFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            hostCtx.Set<RoleMetadata>().Add(RoleMetadata.Create(
                Guid.NewGuid(), "Collide", MultiTenancySide.Both, tenantId: null));
            await hostCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        IGranitRoleOrchestrator orchestrator = scope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await orchestrator.CreateAsync(
                new CreateRoleCommand("Collide", MultiTenancySide.Both, TenantId: null),
                TestContext.Current.CancellationToken));

        // Atomic invariant: the Identity INSERT rolled back with the transaction.
        // There should be ZERO orphan GranitRole — the compensating delete path is
        // NOT invoked (the transaction rollback handled it natively).
        GranitRole? orphan = await roleManager.FindByNameAsync("Collide");
        orphan.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_HappyPath_UpdatesBothRowsAtomically()
    {
        // Seed via orchestrator happy path first.
        await using AsyncServiceScope createScope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator createOrchestrator = createScope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleMetadata created = await createOrchestrator.CreateAsync(
            new CreateRoleCommand("Alpha", MultiTenancySide.Both, TenantId: null),
            TestContext.Current.CancellationToken);

        // Rename in a fresh scope (mirrors production request-per-scope semantics).
        await using AsyncServiceScope renameScope = _app.Services.CreateAsyncScope();
        IGranitRoleOrchestrator renameOrchestrator = renameScope.ServiceProvider
            .GetRequiredService<IGranitRoleOrchestrator>();
        RoleManager<GranitRole> roleManager = renameScope.ServiceProvider
            .GetRequiredService<RoleManager<GranitRole>>();
        IRoleMetadataStore store = renameScope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        await renameOrchestrator.RenameAsync(
            created.Id, newName: "AlphaPrime", newDescription: "renamed",
            TestContext.Current.CancellationToken);

        GranitRole? identityRole = await roleManager.FindByIdAsync(created.Id.ToString("D"));
        identityRole.ShouldNotBeNull();
        identityRole.Name.ShouldBe("AlphaPrime");

        RoleMetadata? metadata = await store.FindByIdAsync(created.Id, TestContext.Current.CancellationToken);
        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("AlphaPrime");
        metadata.Description.ShouldBe("renamed");
    }
}
