// =============================================================================
// Tests - EfCorePermissionGrantStore
// =============================================================================
// Vérifie que le store :
//   - Retourne true pour un grant existant
//   - Retourne false si le grantee (ProviderName/ProviderKey), la permission ou le tenant
//     ne correspondent pas
//   - Gère correctement les grants à portée globale (TenantId null)
// Les grants sont scopés via le tuple ABP-style (ProviderName, ProviderKey) où
// ProviderName="R" identifie un grantee de type rôle.
// =============================================================================

using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Stores;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class EfCorePermissionGrantStoreTests
{
    private const string R = PermissionGrantProviderNames.Role;
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public async Task IsGrantedAsync_MatchingGrant_ReturnsTrue()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(R, "accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentRole_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(R, "reader", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentPermission_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(R, "accountant", "Invoices.Read", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentTenant_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(R, "accountant", "Invoices.Delete", TenantB, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DifferentProvider_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        // Same textual key but a different provider — must not match.
        bool result = await store.IsGrantedAsync(
            PermissionGrantProviderNames.User, "accountant", "Invoices.Delete", TenantA,
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_GlobalGrant_NullTenantMatches()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "admin", "System.Configure", tenantId: null);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(
            R, "admin", "System.Configure", tenantId: null, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsGrantedAsync_GlobalGrantDoesNotMatchTenant_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "admin", "System.Configure", tenantId: null);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.IsGrantedAsync(R, "admin", "System.Configure", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // --- GetGrantedPermissionsAsync ---

    [Fact]
    public async Task GetGrantedPermissionsAsync_ReturnsPermissionsForGrantee()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        await SeedAsync(context, "accountant", "Invoices.Read", TenantA);
        await SeedAsync(context, "reader", "Invoices.Read", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        IReadOnlyList<string> permissions =
            await store.GetGrantedPermissionsAsync(R, "accountant", TenantA, TestContext.Current.CancellationToken);

        permissions.ShouldBe(["Invoices.Delete", "Invoices.Read"]);
    }

    // --- GetGranteesAsync ---

    [Fact]
    public async Task GetGranteesAsync_ReturnsGranteesForPermission()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        await SeedAsync(context, "manager", "Invoices.Delete", TenantA);
        await SeedAsync(context, "reader", "Invoices.Read", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        IReadOnlyList<string> grantees =
            await store.GetGranteesAsync(R, "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        grantees.ShouldBe(["accountant", "manager"]);
    }

    // --- GrantAsync ---

    [Fact]
    public async Task GrantAsync_NewGrant_ReturnsTrueAndPersists()
    {
        await using TestDbContext context = CreateContext();
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.GrantAsync(R, "accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == "Invoices.Delete"
                && g.ProviderName == R
                && g.ProviderKey == "accountant"
                && g.TenantId == TenantA,
            TestContext.Current.CancellationToken);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task GrantAsync_AlreadyExists_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.GrantAsync(R, "accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // --- RevokeAsync ---

    [Fact]
    public async Task RevokeAsync_ExistingGrant_ReturnsTrueAndRemoves()
    {
        await using TestDbContext context = CreateContext();
        await SeedAsync(context, "accountant", "Invoices.Delete", TenantA);
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.RevokeAsync(R, "accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        bool exists = await context.PermissionGrants.AnyAsync(
            g => g.Name == "Invoices.Delete"
                && g.ProviderName == R
                && g.ProviderKey == "accountant"
                && g.TenantId == TenantA,
            TestContext.Current.CancellationToken);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NotExists_ReturnsFalse()
    {
        await using TestDbContext context = CreateContext();
        EfCorePermissionGrantStore<TestDbContext> store = new(context, new SimpleGuidGenerator());

        bool result = await store.RevokeAsync(R, "accountant", "Invoices.Delete", TenantA, TestContext.Current.CancellationToken);

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
            ProviderName = R,
            ProviderKey = roleName,
            TenantId = tenantId
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
