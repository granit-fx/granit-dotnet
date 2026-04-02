using Granit.Identity.Federated.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests.Events;

public sealed class EfCoreIdentityEventsTests
{
    // ──── IdentityUserUpdatedEto ────

    [Fact]
    public void IdentityUserUpdatedEto_SetsUserId()
    {
        var evt = new IdentityUserUpdatedEto("user-1");

        evt.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void IdentityUserUpdatedEto_SupportsRecordEquality()
    {
        var evt1 = new IdentityUserUpdatedEto("user-1");
        var evt2 = new IdentityUserUpdatedEto("user-1");

        evt1.ShouldBe(evt2);
    }

    // ──── IdentityUserDeletedEto ────

    [Fact]
    public void IdentityUserDeletedEto_SetsUserId()
    {
        var evt = new IdentityUserDeletedEto("user-1");

        evt.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void IdentityUserDeletedEto_TenantIdDefaultsToNull()
    {
        var evt = new IdentityUserDeletedEto("user-1");

        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void IdentityUserDeletedEto_AcceptsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var evt = new IdentityUserDeletedEto("user-1", tenantId);

        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void IdentityUserDeletedEto_SupportsRecordEquality()
    {
        var tenantId = Guid.NewGuid();
        var evt1 = new IdentityUserDeletedEto("user-1", tenantId);
        var evt2 = new IdentityUserDeletedEto("user-1", tenantId);

        evt1.ShouldBe(evt2);
    }
}
