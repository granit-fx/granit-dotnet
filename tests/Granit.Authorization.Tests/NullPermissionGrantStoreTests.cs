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
    [Fact]
    public async Task IsGrantedAsync_AlwaysReturnsFalse()
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        // Act
        bool result = await store.IsGrantedAsync(
            "admin",
            "Invoices.Delete",
            tenantId: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse("NullPermissionGrantStore denies every permission by design");
    }

    [Theory]
    [InlineData("admin", "Invoices.Read", null)]
    [InlineData("editor", "Products.Create", "00000000-0000-0000-0000-000000000001")]
    [InlineData("viewer", "Reports.Export", "00000000-0000-0000-0000-000000000002")]
    public async Task IsGrantedAsync_AnyArguments_AlwaysReturnsFalse(
        string roleName,
        string permissionName,
        string? tenantIdString)
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);
        Guid? tenantId = tenantIdString is null ? null : Guid.Parse(tenantIdString);

        // Act
        bool result = await store.IsGrantedAsync(
            roleName,
            permissionName,
            tenantId,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGrantedPermissionsAsync_ReturnsEmpty()
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        // Act
        IReadOnlyList<string> result = await store.GetGrantedPermissionsAsync(
            "admin", tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetGrantedRolesAsync_ReturnsEmpty()
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        // Act
        IReadOnlyList<string> result = await store.GetGrantedRolesAsync(
            "Invoices.Delete", tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GrantAsync_ReturnsFalse()
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        // Act
        bool result = await store.GrantAsync(
            "Invoices.Delete", "admin", tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalse()
    {
        // Arrange
        NullPermissionGrantStore store = new(NullLogger<NullPermissionGrantStore>.Instance);

        // Act
        bool result = await store.RevokeAsync(
            "Invoices.Delete", "admin", tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }
}
