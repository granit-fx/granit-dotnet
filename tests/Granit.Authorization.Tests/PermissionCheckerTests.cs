// =============================================================================
// Tests - PermissionChecker
// =============================================================================
// Verifies the full RBAC pipeline:
//   1. Not authenticated → false
//   2. AlwaysAllow (authenticated only) → true without store or cache
//   3. AdminRole bypass (case-insensitive) → true without store or cache
//   4. Unknown permission → InvalidOperationException
//   5. Cache miss → store called, result cached
//   6. Cache hit → store NOT called
//   7. Multi-role OR logic
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Authorization;
using Granit.Authorization.Cache;
using Granit.Authorization.Diagnostics;
using Granit.Authorization.Options;
using Granit.Authorization.Services;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Tests;

public sealed class PermissionCheckerTests
{
    private const string DefinedPermission = "Invoices.Delete";
    private const string UndefinedPermission = "Unknown.Permission";
    private const string AdminRoleName = "admin";
    private static readonly Guid TenantId = Guid.NewGuid();

    // --- AlwaysAllow ---

    [Fact]
    public async Task IsGrantedAsync_AlwaysAllow_ReturnsTrueWithoutStoreOrCache()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        PermissionChecker checker = BuildChecker(
            alwaysAllow: true,
            isAuthenticated: true,
            cache: cache,
            store: store);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- Not authenticated ---

    [Fact]
    public async Task IsGrantedAsync_NotAuthenticated_ReturnsFalse()
    {
        // Arrange
        PermissionChecker checker = BuildChecker(isAuthenticated: false);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- AdminRole bypass ---

    [Fact]
    public async Task IsGrantedAsync_UserHasAdminRole_ReturnsTrueWithoutStoreOrCache()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: [AdminRoleName],
            cache: cache,
            store: store);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- Undefined permission ---

    [Fact]
    public async Task IsGrantedAsync_UndefinedPermission_ThrowsInvalidOperationException()
    {
        // Arrange
        PermissionChecker checker = BuildChecker(isAuthenticated: true, roles: ["editor"]);

        // Act
        Func<Task> act = () => checker.IsGrantedAsync(UndefinedPermission);

        // Assert
        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldContain($"'{UndefinedPermission}'");
    }

    // --- Cache miss → store called ---

    [Fact]
    public async Task IsGrantedAsync_CacheMiss_StoreCalledAndGrantCached()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("R", "editor", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        IFusionCache cache = BuildPassThroughCache();

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["editor"],
            tenantId: TenantId,
            store: store,
            cache: cache);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.Received(1).IsGrantedAsync(
            "R", "editor", DefinedPermission, TenantId, Arg.Any<CancellationToken>());
    }

    // --- Cache hit → store NOT called ---

    [Fact]
    public async Task IsGrantedAsync_CacheHit_StoreNotCalled()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();

        IFusionCache cache = Substitute.For<IFusionCache>();
        cache.GetOrSetAsync<PermissionGrantCacheItem>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<PermissionGrantCacheItem>, CancellationToken, Task<PermissionGrantCacheItem>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new PermissionGrantCacheItem(true));

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["editor"],
            store: store,
            cache: cache);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- Multi-role OR logic ---

    [Fact]
    public async Task IsGrantedAsync_SecondRoleHasGrant_ReturnsTrue()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("R", "reader", DefinedPermission, null, Arg.Any<CancellationToken>())
            .Returns(false);
        store.IsGrantedAsync("R", "editor", DefinedPermission, null, Arg.Any<CancellationToken>())
            .Returns(true);

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["reader", "editor"],
            store: store,
            cache: BuildPassThroughCache());

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsGrantedAsync_NoRoleHasGrant_ReturnsFalse()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("R", Arg.Any<string>(), DefinedPermission, null, Arg.Any<CancellationToken>())
            .Returns(false);

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["reader", "editor"],
            store: store,
            cache: BuildPassThroughCache());

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- BuildCacheKey ---

    [Fact]
    public void BuildCacheKey_WithTenantId_FormatsCorrectly()
    {
        var tenant = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        string key = PermissionChecker.BuildCacheKey(tenant, "R", "editor", "Invoices.Delete");
        key.ShouldBe($"perm:{tenant}:R:editor:Invoices.Delete");
    }

    [Fact]
    public void BuildCacheKey_WithoutTenantId_UsesGlobalSegment()
    {
        string key = PermissionChecker.BuildCacheKey(null, "R", "editor", "Invoices.Delete");
        key.ShouldBe("perm:global:R:editor:Invoices.Delete");
    }

    // --- AdminRole bypass (case-insensitive) ---

    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("aDmIn")]
    public async Task IsGrantedAsync_AdminRoleCaseInsensitive_ReturnsTrueRegardlessOfCasing(string roleCasing)
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: [roleCasing],
            cache: cache,
            store: store);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- AlwaysAllow requires authentication ---

    [Fact]
    public async Task IsGrantedAsync_AlwaysAllowButNotAuthenticated_ReturnsFalse()
    {
        // Arrange — AlwaysAllow must NOT bypass authentication check.
        PermissionChecker checker = BuildChecker(
            alwaysAllow: true,
            isAuthenticated: false);

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- Tenant-aware cache key ---

    [Fact]
    public async Task IsGrantedAsync_WithTenant_PassesTenantIdToStore()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("R", "editor", DefinedPermission, TenantId, Arg.Any<CancellationToken>())
            .Returns(true);

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["editor"],
            tenantId: TenantId,
            store: store,
            cache: BuildPassThroughCache());

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.Received(1).IsGrantedAsync(
            "R", "editor", DefinedPermission, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsGrantedAsync_WithoutTenant_PassesNullToStore()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("R", "editor", DefinedPermission, null, Arg.Any<CancellationToken>())
            .Returns(true);

        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: ["editor"],
            tenantId: null,
            store: store,
            cache: BuildPassThroughCache());

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.Received(1).IsGrantedAsync(
            "R", "editor", DefinedPermission, null, Arg.Any<CancellationToken>());
    }

    // --- AdminRole with multiple admin roles configured ---

    [Fact]
    public async Task IsGrantedAsync_MultipleAdminRolesConfigured_AnyMatchReturnsTrue()
    {
        // Arrange
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.IsAuthenticated.Returns(true);
        user.GetRoles().Returns(["superadmin"]);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists(DefinedPermission).Returns(true);
        manager.Find(DefinedPermission)
            .Returns(new PermissionDefinition(DefinedPermission, null, "TestGroup", MultiTenancySides.Both));

        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        IFusionCache cache = Substitute.For<IFusionCache>();

        GranitAuthorizationOptions opts = new()
        {
            AdminRoles = [AdminRoleName, "superadmin", "root"],
            CacheDuration = TimeSpan.FromMinutes(5)
        };

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        AuthorizationMetrics metrics = new(meterFactory);

        PermissionChecker checker = new(user, tenant, manager, store, BuildDefaultProviders(), cache, metrics,
            Microsoft.Extensions.Options.Options.Create(opts));

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- Empty roles list ---

    [Fact]
    public async Task IsGrantedAsync_EmptyRolesList_ReturnsFalse()
    {
        // Arrange
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        PermissionChecker checker = BuildChecker(
            isAuthenticated: true,
            roles: [],
            store: store,
            cache: BuildPassThroughCache());

        // Act
        bool result = await checker.IsGrantedAsync(DefinedPermission, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- BuildCacheKey edge cases ---

    [Fact]
    public void BuildCacheKey_EmptyRoleName_IncludesEmptySegment()
    {
        string key = PermissionChecker.BuildCacheKey(null, "R", "", "Invoices.Delete");

        key.ShouldBe("perm:global:R::Invoices.Delete");
    }

    [Fact]
    public void BuildCacheKey_EmptyPermissionName_IncludesEmptySegment()
    {
        string key = PermissionChecker.BuildCacheKey(null, "R", "editor", "");

        key.ShouldBe("perm:global:R:editor:");
    }

    // --- Helpers ---

    private static PermissionChecker BuildChecker(
        bool alwaysAllow = false,
        bool isAuthenticated = true,
        string[]? roles = null,
        Guid? tenantId = null,
        IPermissionGrantStore? store = null,
        IFusionCache? cache = null)
    {
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.IsAuthenticated.Returns(isAuthenticated);
        user.GetRoles().Returns((roles ?? []).ToList().AsReadOnly());
        user.IsInRole(Arg.Any<string>()).Returns(false);
        foreach (string role in roles ?? [])
        {
            user.IsInRole(role).Returns(true);
        }

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(tenantId.HasValue);
        tenant.Id.Returns(tenantId);

        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists(DefinedPermission).Returns(true);
        manager.Exists(UndefinedPermission).Returns(false);
        manager.Find(DefinedPermission)
            .Returns(new PermissionDefinition(DefinedPermission, null, "TestGroup", MultiTenancySides.Both));
        manager.Find(UndefinedPermission).Returns((PermissionDefinition?)null);

        IPermissionGrantStore grantStore = store ?? Substitute.For<IPermissionGrantStore>();
        IFusionCache cacheService = cache ?? BuildPassThroughCache();

        GranitAuthorizationOptions opts = new()
        {
            AdminRoles = [AdminRoleName],
            AlwaysAllow = alwaysAllow,
            CacheDuration = TimeSpan.FromMinutes(5)
        };

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        AuthorizationMetrics metrics = new(meterFactory);

        return new PermissionChecker(user, tenant, manager, grantStore, BuildDefaultProviders(), cacheService, metrics, Microsoft.Extensions.Options.Options.Create(opts));
    }

    /// <summary>
    /// Default U → R → C provider chain matching the registration order in
    /// <c>AuthorizationServiceCollectionExtensions.AddGranitAuthorization</c>.
    /// </summary>
    private static IEnumerable<IPermissionGrantProvider> BuildDefaultProviders() =>
    [
        new UserPermissionGrantProvider(),
        new RolePermissionGrantProvider(),
        new ClientPermissionGrantProvider(),
    ];

    /// <summary>
    /// Cache substitute that always calls the factory (simulates a cache miss on every call).
    /// Uses a real in-memory FusionCache instance to avoid complex mock setup.
    /// </summary>
    private static FusionCache BuildPassThroughCache() =>
        new(new FusionCacheOptions());
}
