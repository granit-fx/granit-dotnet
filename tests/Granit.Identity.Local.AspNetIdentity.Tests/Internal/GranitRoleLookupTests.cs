using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Internal;

public sealed class GranitRoleLookupTests
{
    private readonly IRoleMetadataStore _store = Substitute.For<IRoleMetadataStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    // ─────────────────────────────────────────────────────────────────────
    // Host context — never consults the tenant-scoped row
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindByNameAsync_HostContext_QueriesHostScopeOnly()
    {
        _currentTenant.IsAvailable.Returns(false);
        var hostRole = RoleMetadata.Create(
            Guid.NewGuid(), "SuperAdmin", MultiTenancySide.Host, tenantId: null);
        _store.FindByNameAsync("SuperAdmin", null, null, Arg.Any<CancellationToken>())
            .Returns(hostRole);

        GranitRoleLookup lookup = new(_store, _currentTenant);

        RoleMetadata? result = await lookup.FindByNameAsync(
            "SuperAdmin", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(hostRole);

        // Never asks the store with a specific tenantId when outside a tenant context.
        await _store.DidNotReceive().FindByNameAsync(
            Arg.Any<string>(), Arg.Is<Guid?>(id => id != null),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Tenant context — tenant-scope row wins, Both-scope row is the fallback
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindByNameAsync_TenantContext_TenantScopeRole_TakesPrecedence()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        var tenantRole = RoleMetadata.Create(
            Guid.NewGuid(), "Manager", MultiTenancySide.Tenant, tenantId);
        _store.FindByNameAsync("Manager", tenantId, null, Arg.Any<CancellationToken>())
            .Returns(tenantRole);

        GranitRoleLookup lookup = new(_store, _currentTenant);

        RoleMetadata? result = await lookup.FindByNameAsync(
            "Manager", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(tenantRole);

        // The fallback to host-scope row is not needed.
        await _store.DidNotReceive().FindByNameAsync(
            "Manager", (Guid?)null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByNameAsync_TenantContext_NoTenantScopeRow_FallsBackToBothScope()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        var bothRole = RoleMetadata.Create(
            Guid.NewGuid(), "User", MultiTenancySide.Both, tenantId: null);
        _store.FindByNameAsync("User", tenantId, null, Arg.Any<CancellationToken>())
            .Returns((RoleMetadata?)null);
        _store.FindByNameAsync("User", (Guid?)null, null, Arg.Any<CancellationToken>())
            .Returns(bothRole);

        GranitRoleLookup lookup = new(_store, _currentTenant);

        RoleMetadata? result = await lookup.FindByNameAsync(
            "User", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(bothRole);
    }

    [Fact]
    public async Task FindByNameAsync_TenantContext_NoMatch_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        _store.FindByNameAsync("Ghost", Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((RoleMetadata?)null);

        GranitRoleLookup lookup = new(_store, _currentTenant);

        RoleMetadata? result = await lookup.FindByNameAsync(
            "Ghost", cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ─────────────────────────────────────────────────────────────────────
    // ClientId propagates through both probes (reserved for realm/client distinction)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindByNameAsync_WithClientId_PropagatesToStoreOnBothProbes()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        _store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((RoleMetadata?)null);

        GranitRoleLookup lookup = new(_store, _currentTenant);

        await lookup.FindByNameAsync(
            "ClientRole", clientId: "showcase-admin",
            cancellationToken: TestContext.Current.CancellationToken);

        await _store.Received(1).FindByNameAsync(
            "ClientRole", tenantId, "showcase-admin", Arg.Any<CancellationToken>());
        await _store.Received(1).FindByNameAsync(
            "ClientRole", (Guid?)null, "showcase-admin", Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Guards
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindByNameAsync_NullOrWhitespace_Throws(string? name)
    {
        GranitRoleLookup lookup = new(_store, _currentTenant);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await lookup.FindByNameAsync(name!, cancellationToken: TestContext.Current.CancellationToken));
    }
}
