using Granit.OpenIddict.Endpoints.Endpoints;
using Granit.Testing.Fakes;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests;

/// <summary>
/// The admin write path resolves an OIDC application's owning tenant from the request and the
/// ambient tenant scope: an explicit tenant is honoured in host context, the active tenant is
/// inherited otherwise, and a tenant-scoped caller cannot provision for a different tenant.
/// </summary>
public sealed class AdminOidcTenantResolutionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void HostContext_NoExplicit_ResolvesGlobal()
    {
        bool ok = AdminOidcEndpoints.TryResolveWriteTenant(
            explicitTenantId: null, HostContext(), out Guid? resolved);

        ok.ShouldBeTrue();
        resolved.ShouldBeNull();
    }

    [Fact]
    public void HostContext_ExplicitTenant_ResolvesThatTenant()
    {
        bool ok = AdminOidcEndpoints.TryResolveWriteTenant(
            explicitTenantId: TenantA, HostContext(), out Guid? resolved);

        ok.ShouldBeTrue();
        resolved.ShouldBe(TenantA);
    }

    [Fact]
    public void TenantScope_NoExplicit_InheritsActiveTenant()
    {
        bool ok = AdminOidcEndpoints.TryResolveWriteTenant(
            explicitTenantId: null, TenantScope(TenantA), out Guid? resolved);

        ok.ShouldBeTrue();
        resolved.ShouldBe(TenantA);
    }

    [Fact]
    public void TenantScope_ExplicitMatchesActive_IsAllowed()
    {
        bool ok = AdminOidcEndpoints.TryResolveWriteTenant(
            explicitTenantId: TenantA, TenantScope(TenantA), out Guid? resolved);

        ok.ShouldBeTrue();
        resolved.ShouldBe(TenantA);
    }

    [Fact]
    public void TenantScope_ExplicitDiffersFromActive_IsForbidden()
    {
        bool ok = AdminOidcEndpoints.TryResolveWriteTenant(
            explicitTenantId: TenantB, TenantScope(TenantA), out Guid? resolved);

        ok.ShouldBeFalse("a tenant-scoped admin must not provision for another tenant");
        resolved.ShouldBeNull();
    }

    private static FakeCurrentTenant HostContext() => new();

    private static FakeCurrentTenant TenantScope(Guid tenantId) => new() { Id = tenantId };
}
