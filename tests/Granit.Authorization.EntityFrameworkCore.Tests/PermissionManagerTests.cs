// =============================================================================
// Tests - PermissionManager
// =============================================================================
// Vérifie que le manager :
//   - Crée un grant + publie PermissionGrantChangedEvent + émet un log [AUDIT] lors de SetAsync(true)
//   - Supprime le grant + publie PermissionGrantChangedEvent + émet un log [AUDIT] lors de SetAsync(false)
//   - Est no-op si l'état est déjà celui demandé (pas d'écriture DB, pas d'événement)
//   - Lève InvalidOperationException pour une permission non définie
//   - Retourne les permissions accordées à un rôle
//   - Retourne les rôles ayant accès à une permission
// =============================================================================

using Granit.Authorization.Abstractions;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Granit.Authorization.EntityFrameworkCore.Services;
using Granit.Authorization.Events;
using Granit.Core.Events;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class PermissionManagerTests
{
    private const string DefinedPermission = "Invoices.Delete";
    private const string UndefinedPermission = "Unknown.Permission";
    private static readonly Guid TenantId = Guid.NewGuid();

    // --- SetAsync: grant ---

    [Fact]
    public async Task SetAsync_GrantNew_CreatesGrantPublishesEventAndLogsAudit()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager,
            ILocalEventBus eventBus,
            ILogger<PermissionManager<TestDbContext>> logger) = BuildManager();

        // Act
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

        // Assert — grant created
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == DefinedPermission && g.RoleName == "accountant" && g.TenantId == TenantId,
            TestContext.Current.CancellationToken);
        exists.ShouldBeTrue();

        // Assert — event published for cache invalidation
        await eventBus.Received(1).PublishAsync(
            Arg.Is<PermissionGrantChangedEvent>(e =>
                e.PermissionName == DefinedPermission &&
                e.RoleName == "accountant" &&
                e.TenantId == TenantId &&
                e.IsGranted),
            Arg.Any<CancellationToken>());

        // Assert — ISO 27001 audit log emitted
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- SetAsync: revoke ---

    [Fact]
    public async Task SetAsync_RevokeExisting_RemovesGrantPublishesEventAndLogsAudit()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager,
            ILocalEventBus eventBus,
            ILogger<PermissionManager<TestDbContext>> logger) = BuildManager();

        await SeedAsync(context, "accountant", DefinedPermission, TenantId);

        // Act
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: false, TestContext.Current.CancellationToken);

        // Assert — grant removed
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == DefinedPermission && g.RoleName == "accountant" && g.TenantId == TenantId,
            TestContext.Current.CancellationToken);
        exists.ShouldBeFalse();

        // Assert — event published for cache invalidation
        await eventBus.Received(1).PublishAsync(
            Arg.Is<PermissionGrantChangedEvent>(e =>
                e.PermissionName == DefinedPermission &&
                e.RoleName == "accountant" &&
                e.TenantId == TenantId &&
                !e.IsGranted),
            Arg.Any<CancellationToken>());

        // Assert — audit log emitted
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- SetAsync: no-op ---

    [Fact]
    public async Task SetAsync_GrantAlreadyExists_NoOpNoEventNoLog()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager,
            ILocalEventBus eventBus,
            ILogger<PermissionManager<TestDbContext>> logger) = BuildManager();

        await SeedAsync(context, "accountant", DefinedPermission, TenantId);

        // Act — same state as existing grant
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

        // Assert — no extra grants created
        int count = await context.PermissionGrants.CountAsync(
            g => g.Name == DefinedPermission && g.RoleName == "accountant",
            TestContext.Current.CancellationToken);
        count.ShouldBe(1);

        // Assert — no event published (no-op)
        await eventBus.DidNotReceive().PublishAsync(
            Arg.Any<PermissionGrantChangedEvent>(), Arg.Any<CancellationToken>());

        // Assert — no audit log (no-op)
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- SetAsync: undefined permission ---

    [Fact]
    public async Task SetAsync_UndefinedPermission_ThrowsWithoutDatabaseWrite()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager, _, _) = BuildManager();

        // Act
        Func<Task> act = () => manager.SetAsync(UndefinedPermission, "accountant", TenantId, isGranted: true);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain($"'{UndefinedPermission}'");

        int count = await context.PermissionGrants.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(0);
    }

    // --- GetGrantedPermissionsAsync ---

    [Fact]
    public async Task GetGrantedPermissionsAsync_ReturnsPermissionsForRole()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager, _, _) = BuildManager();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantId);
        await SeedAsync(context, "accountant", "Invoices.Read", TenantId);
        await SeedAsync(context, "reader", "Invoices.Read", TenantId); // different role, should not appear

        // Act
        IReadOnlyList<string> permissions =
            await manager.GetGrantedPermissionsAsync("accountant", TenantId, TestContext.Current.CancellationToken);

        // Assert
        permissions.ShouldBe(["Invoices.Delete", "Invoices.Read"]);
    }

    // --- GetGrantedRolesAsync ---

    [Fact]
    public async Task GetGrantedRolesAsync_ReturnsRolesForPermission()
    {
        // Arrange
        (TestDbContext context, PermissionManager<TestDbContext> manager, _, _) = BuildManager();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantId);
        await SeedAsync(context, "manager", "Invoices.Delete", TenantId);
        await SeedAsync(context, "reader", "Invoices.Read", TenantId); // different permission, should not appear

        // Act
        IReadOnlyList<string> roles =
            await manager.GetGrantedRolesAsync("Invoices.Delete", TenantId, TestContext.Current.CancellationToken);

        // Assert
        roles.ShouldBe(["accountant", "manager"]);
    }

    // --- Helpers ---

    private static (TestDbContext, PermissionManager<TestDbContext>,
        ILocalEventBus,
        ILogger<PermissionManager<TestDbContext>>) BuildManager()
    {
        TestDbContext context = new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        IPermissionDefinitionManager definitionManager = Substitute.For<IPermissionDefinitionManager>();
        definitionManager.Exists(DefinedPermission).Returns(true);
        definitionManager.Exists(UndefinedPermission).Returns(false);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();

        ILogger<PermissionManager<TestDbContext>> logger =
            Substitute.For<ILogger<PermissionManager<TestDbContext>>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        PermissionManager<TestDbContext> manager = new(
            context,
            definitionManager,
            eventBus,
            new SimpleGuidGenerator(),
            logger);

        return (context, manager, eventBus, logger);
    }

    private static async Task SeedAsync(
        TestDbContext context,
        string roleName,
        string permissionName,
        Guid? tenantId)
    {
        context.PermissionGrants.Add(new PermissionGrant
        {
            Id = Guid.NewGuid(),
            Name = permissionName,
            RoleName = roleName,
            TenantId = tenantId
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

}
