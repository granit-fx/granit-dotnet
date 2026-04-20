// =============================================================================
// Tests - PermissionManager
// =============================================================================
// Verifies that the manager:
//   - Validates permission definition before store access
//   - Delegates grant/revoke to IPermissionGrantStore using the
//     (providerName, providerKey) tuple
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
    private const string R = PermissionGrantProviderNames.Role;
    private static readonly Guid TenantId = Guid.NewGuid();

    // --- SetAsync: grant ---

    [Fact]
    public async Task SetAsync_GrantNew_DelegatesToStorePublishesEventAndLogsAudit()
    {
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.GrantAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        await manager.SetAsync(DefinedPermission, R, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

        await store.Received(1).GrantAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>());

        await eventBus.Received(1).PublishAsync(
            Arg.Is<PermissionGrantChangedEvent>(e =>
                e.PermissionName == DefinedPermission &&
                e.ProviderName == R &&
                e.ProviderKey == "accountant" &&
                e.TenantId == TenantId &&
                e.IsGranted),
            Arg.Any<CancellationToken>());

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
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.RevokeAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        await manager.SetAsync(DefinedPermission, R, "accountant", TenantId, isGranted: false, TestContext.Current.CancellationToken);

        await store.Received(1).RevokeAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>());

        await eventBus.Received(1).PublishAsync(
            Arg.Is<PermissionGrantChangedEvent>(e =>
                e.PermissionName == DefinedPermission &&
                e.ProviderName == R &&
                e.ProviderKey == "accountant" &&
                e.TenantId == TenantId &&
                !e.IsGranted),
            Arg.Any<CancellationToken>());

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
        (PermissionManager manager, IPermissionGrantStore store,
            ILocalEventBus eventBus, ILogger<PermissionManager> logger) = BuildManager();

        store.GrantAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(false); // store reports no change (already existed)

        await manager.SetAsync(DefinedPermission, R, "accountant", TenantId, isGranted: true, TestContext.Current.CancellationToken);

        await eventBus.DidNotReceive().PublishAsync(
            Arg.Any<PermissionGrantChangedEvent>(), Arg.Any<CancellationToken>());

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
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();

        Func<Task> act = () => manager.SetAsync(UndefinedPermission, R, "accountant", TenantId, isGranted: true);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain($"'{UndefinedPermission}'");

        await store.DidNotReceive().GrantAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- SetAsync: validator rejects ---

    [Fact]
    public async Task SetAsync_GrantRejectedByValidator_ThrowsWithoutStoreAccess()
    {
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager(
            validators:
            [
                new StubValidator(PermissionGrantValidationResult.Reject("test_reject", "nope"))
            ]);

        Func<Task> act = () => manager.SetAsync(DefinedPermission, R, "accountant", TenantId, isGranted: true);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain("test_reject");

        await store.DidNotReceive().GrantAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_RevokeBypassesValidators()
    {
        // Validators only gate additions; revocations must always proceed so that a grant made
        // under relaxed rules can still be removed after the policy tightens.
        StubValidator hostile = new(PermissionGrantValidationResult.Reject("would_block", "should not fire"));
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager(validators: [hostile]);

        store.RevokeAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        await manager.SetAsync(DefinedPermission, R, "accountant", TenantId, isGranted: false, TestContext.Current.CancellationToken);

        hostile.CallCount.ShouldBe(0);
        await store.Received(1).RevokeAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>());
    }

    // --- IsGrantedAsync ---

    [Fact]
    public async Task IsGrantedAsync_DelegatesToStore()
    {
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        store.IsGrantedAsync(R, "accountant", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        bool result = await manager.IsGrantedAsync(DefinedPermission, R, "accountant", TenantId, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    // --- GetGrantedPermissionsAsync ---

    [Fact]
    public async Task GetGrantedPermissionsAsync_DelegatesToStore()
    {
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        IReadOnlyList<string> expected = ["Invoices.Delete", "Invoices.Read"];
        store.GetGrantedPermissionsAsync(R, "accountant", TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        IReadOnlyList<string> permissions =
            await manager.GetGrantedPermissionsAsync(R, "accountant", TenantId, TestContext.Current.CancellationToken);

        permissions.ShouldBe(expected);
    }

    // --- GetGranteesAsync ---

    [Fact]
    public async Task GetGranteesAsync_DelegatesToStore()
    {
        (PermissionManager manager, IPermissionGrantStore store, _, _) = BuildManager();
        IReadOnlyList<string> expected = ["accountant", "manager"];
        store.GetGranteesAsync(R, DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(expected);

        IReadOnlyList<string> grantees =
            await manager.GetGranteesAsync(R, DefinedPermission, TenantId, TestContext.Current.CancellationToken);

        grantees.ShouldBe(expected);
    }

    // --- Helpers ---

    private static (PermissionManager, IPermissionGrantStore,
        ILocalEventBus, ILogger<PermissionManager>) BuildManager(
            IEnumerable<IPermissionGrantValidator>? validators = null)
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();

        IPermissionDefinitionManager definitionManager = Substitute.For<IPermissionDefinitionManager>();
        definitionManager.Exists(DefinedPermission).Returns(true);
        definitionManager.Exists(UndefinedPermission).Returns(false);
        definitionManager.Find(DefinedPermission)
            .Returns(new PermissionDefinition(DefinedPermission, null, "TestGroup", MultiTenancySide.Both));
        definitionManager.Find(UndefinedPermission).Returns((PermissionDefinition?)null);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();

        ILogger<PermissionManager> logger = Substitute.For<ILogger<PermissionManager>>();
        logger.IsEnabled(LogLevel.Information).Returns(true);

        PermissionManager manager = new(
            store,
            definitionManager,
            grantValidators: validators ?? [],
            eventBus,
            logger);

        return (manager, store, eventBus, logger);
    }

    private sealed class StubValidator(PermissionGrantValidationResult result) : IPermissionGrantValidator
    {
        public int CallCount { get; private set; }

        public ValueTask<PermissionGrantValidationResult> ValidateAsync(
            PermissionGrantValidationContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
