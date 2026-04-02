// =============================================================================
// Tests - EfCorePermissionGrantStore
// =============================================================================
// Vérifie que le store :
//   - Retourne true pour un grant existant
//   - Retourne false si le rôle, la permission ou le tenant ne correspond pas
//   - Gère correctement les grants à portée globale (TenantId null)
// =============================================================================

using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Granit.Authorization.EntityFrameworkCore.Stores;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class EfCorePermissionGrantStoreTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public async Task IsGrantedAsync_MatchingGrant_ReturnsTrue()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.IsGrantedAsync("accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentRole_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.IsGrantedAsync("reader", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentPermission_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.IsGrantedAsync("accountant", "Invoices.Read", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentTenant_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.IsGrantedAsync("accountant", "Invoices.Delete", TenantB, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_GlobalGrant_NullTenantMatches()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "admin", "System.Configure", tenantId: null);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.IsGrantedAsync("admin", "System.Configure", tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsGrantedAsync_GlobalGrantDoesNotMatchTenant_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "admin", "System.Configure", tenantId: null);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act — tenant-scoped query should not match the global (null) grant
        bool result = await store.IsGrantedAsync("admin", "System.Configure", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- GetGrantedPermissionsAsync ---

    [Fact]
    public async Task GetGrantedPermissionsAsync_ReturnsPermissionsForRole()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        await SeedAsync(context, "accountant", "Invoices.Read", TenantA);
        await SeedAsync(context, "reader", "Invoices.Read", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        IReadOnlyList<string> permissions =
            await store.GetGrantedPermissionsAsync("accountant", TenantA, TestContext.Current.CancellationToken);

        // Assert
        permissions.ShouldBe(["Invoices.Delete", "Invoices.Read"]);
    }

    // --- GetGrantedRolesAsync ---

    [Fact]
    public async Task GetGrantedRolesAsync_ReturnsRolesForPermission()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        await SeedAsync(context, "manager", "Invoices.Delete", TenantA);
        await SeedAsync(context, "reader", "Invoices.Read", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        IReadOnlyList<string> roles =
            await store.GetGrantedRolesAsync("Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        // Assert
        roles.ShouldBe(["accountant", "manager"]);
    }

    // --- GrantAsync ---

    [Fact]
    public async Task GrantAsync_NewGrant_ReturnsTrueAndPersists()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.GrantAsync("Invoices.Delete", "accountant", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == "Invoices.Delete" && g.RoleName == "accountant" && g.TenantId == TenantA,
            TestContext.Current.CancellationToken);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task GrantAsync_AlreadyExists_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.GrantAsync("Invoices.Delete", "accountant", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- RevokeAsync ---

    [Fact]
    public async Task RevokeAsync_ExistingGrant_ReturnsTrueAndRemoves()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.RevokeAsync("Invoices.Delete", "accountant", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == "Invoices.Delete" && g.RoleName == "accountant" && g.TenantId == TenantA,
            TestContext.Current.CancellationToken);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NotExists_ReturnsFalse()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Act
        bool result = await store.RevokeAsync("Invoices.Delete", "accountant", TenantA, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    // --- Helpers ---

    private static TestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
