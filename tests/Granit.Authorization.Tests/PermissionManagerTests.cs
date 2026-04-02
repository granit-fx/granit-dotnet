// =============================================================================
// Tests - PermissionManager
// =============================================================================
// Verifies that the manager:
//   - Validates permission definition before store access
//   - Delegates grant/revoke to IPermissionGrantStore
//   - Publishes PermissionGrantChangedEvent + emits [AUDIT] log on state change
//   - Is no-op when store reports no change (idempotent)
//   - Throws InvalidOperationException for undefined permissions
//   - Delegates read operations to IPermissionGrantStore
// =============================================================================

using Granit.Authorization;
using Granit.Authorization.Events;
using Granit.Authorization.Services;
using Granit.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionManagerTests
{
    private const string DefinedPermission = "Invoices.Delete";
    private const string UndefinedPermission = "Unknown.Permission";
    private static readonly Guid TenantId = Guid.NewGuid();

    // --- SetAsync: grant ---

    [Fact]
    public async Task SetAsync_GrantNew_DelegatesToStorePublishesEventAndLogsAudit()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.GrantAsync(DefinedPermission, "accountant", TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

        // Assert — store called
        await store.Received(1).GrantAsync(DefinedPermission, "accountant", TenantId, Arg.Any<CancellationToken>());

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
    public async Task SetAsync_RevokeExisting_DelegatesToStorePublishesEventAndLogsAudit()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.RevokeAsync(DefinedPermission, "accountant", TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: false, TestContext.Current.CancellationToken);

        // Assert — store called
        await store.Received(1).RevokeAsync(DefinedPermission, "accountant", TenantId, Arg.Any<CancellationToken>());

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
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.GrantAsync(DefinedPermission, "accountant", TenantId, Arg.Any<CancellationToken>())
            .Returns(false); // store reports no change (already existed)

        // Act
        await manager.SetAsync(DefinedPermission, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

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
    public async Task SetAsync_UndefinedPermission_ThrowsWithoutStoreAccess()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();

        // Act
        Func<Task> act = () => manager.SetAsync(UndefinedPermission, "accountant", TenantId, isGranted: true);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain($"'{UndefinedPermission}'");

        await store.DidNotReceive().GrantAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- IsGrantedAsync ---

    [Fact]
    public async Task IsGrantedAsync_DelegatesToStore()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        store.IsGrantedAsync("accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        bool result = await manager.IsGrantedAsync(DefinedPermission, "accountant", TenantId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    // --- GetGrantedPermissionsAsync ---

    [Fact]
    public async Task GetGrantedPermissionsAsync_DelegatesToStore()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        IReadOnlyList<string> expected = ["Invoices.Delete", "Invoices.Read"];
        store.GetGrantedPermissionsAsync("accountant", TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        IReadOnlyList<string> permissions =
            await manager.GetGrantedPermissionsAsync("accountant", TenantId, TestContext.Current.CancellationToken);

        // Assert
        permissions.ShouldBe(expected);
    }

    // --- GetGrantedRolesAsync ---

    [Fact]
    public async Task GetGrantedRolesAsync_DelegatesToStore()
    {
        // Arrange
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        IReadOnlyList<string> expected = ["accountant", "manager"];
        store.GetGrantedRolesAsync(DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        IReadOnlyList<string> roles =
            await manager.GetGrantedRolesAsync(DefinedPermission, TenantId, TestContext.Current.CancellationToken);

        // Assert
        roles.ShouldBe(expected);
    }

    // --- Helpers ---

    private static (PermissionManager, IPermissionGrantStore,
        ILocalEventBus, ILogger<PermissionManager>) BuildManager()
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();

        IPermissionDefinitionManager definitionManager = Substitute.For<IPermissionDefinitionManager>();
        definitionManager.Exists(DefinedPermission).Returns(true);
        definitionManager.Exists(UndefinedPermission).Returns(false);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();

        ILogger<PermissionManager> logger = Substitute.For<ILogger<PermissionManager>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        PermissionManager manager = new(
            store,
            definitionManager,
            eventBus,
            logger);

        return (manager, store, eventBus, logger);
    }
}
