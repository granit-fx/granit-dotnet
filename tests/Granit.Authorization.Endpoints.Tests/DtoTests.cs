using Granit.Authorization;
using Granit.Authorization.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class DtoTests
{
    // ── MyPermissionsResponse ──────────────────────────────────────────────

    [Fact]
    public void MyPermissionsResponse_SetsPermissions()
    {
        IReadOnlyList<string> permissions = ["Invoices.Read", "Invoices.Create"];

        MyPermissionsResponse response = new(permissions);

        response.Permissions.ShouldBe(permissions);
    }

    [Fact]
    public void MyPermissionsResponse_EmptyList_IsValid()
    {
        MyPermissionsResponse response = new([]);

        response.Permissions.ShouldBeEmpty();
    }

    // ── PermissionDefinitionResponse ───────────────────────────────────────

    [Fact]
    public void PermissionDefinitionResponse_SetsAllProperties()
    {
        PermissionDefinitionResponse response = new("Invoices.Read", "Read invoices", MultiTenancySide.Tenant);

        response.Name.ShouldBe("Invoices.Read");
        response.DisplayName.ShouldBe("Read invoices");
        response.MultiTenancySide.ShouldBe(MultiTenancySide.Tenant);
    }

    [Fact]
    public void PermissionDefinitionResponse_NullDisplayName_IsValid()
    {
        PermissionDefinitionResponse response = new("Invoices.Read", null, MultiTenancySide.Both);

        response.DisplayName.ShouldBeNull();
        response.MultiTenancySide.ShouldBe(MultiTenancySide.Both);
    }

    // ── PermissionGroupResponse ────────────────────────────────────────────

    [Fact]
    public void PermissionGroupResponse_SetsAllProperties()
    {
        List<PermissionDefinitionResponse> permissions =
        [
            new("Invoices.Read", "Read invoices", MultiTenancySide.Tenant),
            new("Invoices.Create", "Create invoices", MultiTenancySide.Tenant)
        ];

        PermissionGroupResponse response = new("Invoices", "Invoice Management", permissions);

        response.Name.ShouldBe("Invoices");
        response.DisplayName.ShouldBe("Invoice Management");
        response.Permissions.Count.ShouldBe(2);
    }

    [Fact]
    public void PermissionGroupResponse_NullDisplayName_IsValid()
    {
        PermissionGroupResponse response = new("Invoices", null, []);

        response.DisplayName.ShouldBeNull();
    }

    // ── PermissionGrantResponse ────────────────────────────────────────────

    [Fact]
    public void PermissionGrantResponse_SetsAllProperties()
    {
        IReadOnlyList<string> permissions = ["Invoices.Read", "Invoices.Create"];

        PermissionGrantResponse response = new("editor", permissions);

        response.RoleName.ShouldBe("editor");
        response.Permissions.ShouldBe(permissions);
    }

    [Fact]
    public void PermissionGrantResponse_EmptyPermissions_IsValid()
    {
        PermissionGrantResponse response = new("viewer", []);

        response.RoleName.ShouldBe("viewer");
        response.Permissions.ShouldBeEmpty();
    }
}
