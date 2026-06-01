using Granit.Hostnames.Domain;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests.Domain;

public sealed class ManagedHostnameTests
{
    private static readonly Guid Id = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OwnerId = new("66666666-6666-6666-6666-666666666666");
    private static readonly Guid TenantId = new("77777777-7777-7777-7777-777777777777");

    [Fact]
    public void Create_sets_fields_and_starts_active()
    {
        var hostname = ManagedHostname.Create(
            Id, "acme.com", "cms.site", OwnerId, TenantId, isPrimary: true);

        hostname.Id.ShouldBe(Id);
        hostname.Host.Value.ShouldBe("acme.com");
        hostname.OwnerType.ShouldBe("cms.site");
        hostname.OwnerId.ShouldBe(OwnerId);
        hostname.TenantId.ShouldBe(TenantId);
        hostname.IsPrimary.ShouldBeTrue();
        hostname.Status.ShouldBe(HostnameStatus.Active);
    }

    [Fact]
    public void Create_defaults_tenant_to_null_and_not_primary()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.TenantId.ShouldBeNull();
        hostname.IsPrimary.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_owner_type(string ownerType)
    {
        Should.Throw<ArgumentException>(
            () => ManagedHostname.Create(Id, "acme.com", ownerType, OwnerId));
    }

    [Fact]
    public void Create_rejects_null_host()
    {
        Should.Throw<ArgumentNullException>(
            () => ManagedHostname.Create(Id, null!, "cms.site", OwnerId));
    }

    [Fact]
    public void SetPrimary_and_ClearPrimary_toggle_the_canonical_flag()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.SetPrimary();
        hostname.IsPrimary.ShouldBeTrue();

        hostname.ClearPrimary();
        hostname.IsPrimary.ShouldBeFalse();
    }
}
