// =============================================================================
// Tests - NullPermissionGrantStore
// =============================================================================
// Verifies that the default no-op implementation always denies permissions,
// returns empty collections, and reports no changes on write operations.
// =============================================================================

using Granit.Authorization.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class NullPermissionGrantStoreTests
{
    private const string R = PermissionGrantProviderNames.Role;

    [Fact]
    public async Task IsGrantedAsync_AlwaysReturnsFalse()
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        bool result = await store.IsGrantedAsync(
            R,
            "admin",
            "Invoices.Delete",
            tenantId: null,
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse("NullPermissionGrantStore denies every permission by design");
    }

    [Theory]
    [InlineData("admin", "Invoices.Read", null)]
    [InlineData("editor", "Products.Create", "00000000-0000-0000-0000-000000000001")]
    [InlineData("viewer", "Reports.Export", "00000000-0000-0000-0000-000000000002")]
    public async Task IsGrantedAsync_AnyArguments_AlwaysReturnsFalse(
        string providerKey,
        string permissionName,
        string? tenantIdString)
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);
        Guid? tenantId = tenantIdString is null ? null : Guid.Parse(tenantIdString);

        bool result = await store.IsGrantedAsync(
            R,
            providerKey,
            permissionName,
            tenantId,
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGrantedPermissionsAsync_ReturnsEmpty()
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        IReadOnlyList<string> result = await store.GetGrantedPermissionsAsync(
            R, "admin", tenantId: null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetGranteesAsync_ReturnsEmpty()
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        IReadOnlyList<string> result = await store.GetGranteesAsync(
            R, "Invoices.Delete", tenantId: null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GrantAsync_ReturnsFalse()
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        bool result = await store.GrantAsync(
            R, "admin", "Invoices.Delete", tenantId: null, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalse()
    {
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        bool result = await store.RevokeAsync(
            R, "admin", "Invoices.Delete", tenantId: null, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }
}
