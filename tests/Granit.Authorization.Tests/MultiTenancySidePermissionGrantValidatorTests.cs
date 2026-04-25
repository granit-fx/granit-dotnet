using Granit.Authorization.Domain;
using Granit.Authorization.Services;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class MultiTenancySidePermissionGrantValidatorTests
{
    private const string R = PermissionGrantProviderNames.Role;
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    // ─────────────────────────────────────────────────────────────────────
    // Permission-side checks (existing behavior, unchanged)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantOnlyPermission_HostLevelGrant_Rejected()
    {
        PermissionGrantValidationResult result = await Validate(
            permissionSide: MultiTenancySides.Tenant,
            providerName: R,
            providerKey: "accountant",
            grantTenantId: null);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("side_host_forbidden");
    }

    [Fact]
    public async Task HostOnlyPermission_TenantGrant_Rejected()
    {
        PermissionGrantValidationResult result = await Validate(
            permissionSide: MultiTenancySides.Host,
            providerName: R,
            providerKey: "accountant",
            grantTenantId: TenantA);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("side_tenant_forbidden");
    }

    [Fact]
    public async Task BothPermission_AnyScope_Accepted_WhenNoRoleMetadata()
    {
        (await Validate(MultiTenancySides.Both, R, "accountant", null)).IsValid.ShouldBeTrue();
        (await Validate(MultiTenancySides.Both, R, "accountant", TenantA)).IsValid.ShouldBeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Non-role providers skip role lookups
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserProvider_SkipsRoleMetadataLookup()
    {
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();

        PermissionGrantValidationResult result = await Validate(
            permissionSide: MultiTenancySides.Both,
            providerName: PermissionGrantProviderNames.User,
            providerKey: Guid.NewGuid().ToString(),
            grantTenantId: TenantA,
            store: store);

        result.IsValid.ShouldBeTrue();
        await store.DidNotReceive().FindByNameAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClientProvider_SkipsRoleMetadataLookup()
    {
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();

        PermissionGrantValidationResult result = await Validate(
            permissionSide: MultiTenancySides.Both,
            providerName: PermissionGrantProviderNames.Client,
            providerKey: "client-a",
            grantTenantId: null,
            store: store);

        result.IsValid.ShouldBeTrue();
        await store.DidNotReceive().FindByNameAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Role-side matrix (plan §Validator matrix)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HostRole_HostGrant_Accepted()
    {
        IRoleMetadataStore store = StoreWithRole(HostRole("SuperAdmin"));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Host, R, "SuperAdmin", null, store);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task HostRole_TenantGrant_Rejected()
    {
        IRoleMetadataStore store = StoreWithRole(HostRole("SuperAdmin"));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Both, R, "SuperAdmin", TenantA, store);
        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("role_side_forbidden");
    }

    [Fact]
    public async Task TenantRole_HostGrant_Rejected()
    {
        IRoleMetadataStore store = StoreWithRole(TenantRole("Manager", TenantA));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Both, R, "Manager", null, store);
        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("role_side_forbidden");
    }

    [Fact]
    public async Task TenantRole_MatchingTenantGrant_Accepted()
    {
        IRoleMetadataStore store = StoreWithRole(TenantRole("Manager", TenantA));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Tenant, R, "Manager", TenantA, store);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantRole_MismatchingTenantGrant_Rejected()
    {
        // Role belongs to TenantA; grant targets TenantB.
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();
        store.FindByNameAsync("Manager", TenantB, null, Arg.Any<CancellationToken>())
             .Returns((RoleMetadata?)null);
        store.FindByNameAsync("Manager", null, null, Arg.Any<CancellationToken>())
             .Returns(TenantRole("Manager", TenantA));

        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Tenant, R, "Manager", TenantB, store);
        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("role_tenant_mismatch");
    }

    [Fact]
    public async Task BothRole_HostGrant_Accepted()
    {
        IRoleMetadataStore store = StoreWithRole(BothRole("User"));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Both, R, "User", null, store);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task BothRole_TenantGrant_Accepted()
    {
        IRoleMetadataStore store = StoreWithRole(BothRole("User"));
        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Both, R, "User", TenantA, store);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NoRoleMetadata_FallsThroughToPermissionSideOnly()
    {
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();
        store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
             .Returns((RoleMetadata?)null);

        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Both, R, "legacy-role", TenantA, store);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantScopedLookup_PreferredOverGlobal()
    {
        // Both a global "Manager" (Both side) and a tenant-scoped "Manager" exist.
        // The validator should pick the tenant-scoped one when grant targets that tenant.
        RoleMetadata tenantRole = TenantRole("Manager", TenantA);
        RoleMetadata globalRole = BothRole("Manager");

        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();
        store.FindByNameAsync("Manager", TenantA, null, Arg.Any<CancellationToken>())
             .Returns(tenantRole);
        store.FindByNameAsync("Manager", null, null, Arg.Any<CancellationToken>())
             .Returns(globalRole);

        PermissionGrantValidationResult result = await Validate(MultiTenancySides.Tenant, R, "Manager", TenantA, store);

        result.IsValid.ShouldBeTrue();
        // Assert: only the tenant-scoped lookup was needed (global fallback not called).
        await store.Received(1).FindByNameAsync("Manager", TenantA, null, Arg.Any<CancellationToken>());
        await store.DidNotReceive().FindByNameAsync("Manager", (Guid?)null, null, Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static async Task<PermissionGrantValidationResult> Validate(
        MultiTenancySides permissionSide,
        string providerName,
        string providerKey,
        Guid? grantTenantId,
        IRoleMetadataStore? store = null)
    {
        store ??= Substitute.For<IRoleMetadataStore>();
        var validator = new MultiTenancySidePermissionGrantValidator(store);
        var context = new PermissionGrantValidationContext(
            PermissionName: "Sample.Perm",
            ProviderName: providerName,
            ProviderKey: providerKey,
            TenantId: grantTenantId,
            Definition: new PermissionDefinition("Sample.Perm", null, "Sample", permissionSide));

        return await validator.ValidateAsync(context, TestContext.Current.CancellationToken);
    }

    private static IRoleMetadataStore StoreWithRole(RoleMetadata role)
    {
        // Simulate "the role exists in whatever scope the validator queries" — exercises
        // the decision matrix without coupling the tests to the validator's lookup strategy.
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();
        store.FindByNameAsync(role.Name, Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
             .Returns(role);
        return store;
    }

    private static RoleMetadata HostRole(string name) =>
        RoleMetadata.Create(Guid.NewGuid(), name, MultiTenancySides.Host, tenantId: null);

    private static RoleMetadata BothRole(string name) =>
        RoleMetadata.Create(Guid.NewGuid(), name, MultiTenancySides.Both, tenantId: null);

    private static RoleMetadata TenantRole(string name, Guid tenantId) =>
        RoleMetadata.Create(Guid.NewGuid(), name, MultiTenancySides.Tenant, tenantId);
}
