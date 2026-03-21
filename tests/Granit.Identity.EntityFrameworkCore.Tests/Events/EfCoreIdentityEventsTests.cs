using Granit.Core.Events;
using Granit.Identity.EntityFrameworkCore.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests.Events;

public sealed class EfCoreIdentityEventsTests
{
    // ──── IdentityUserUpdatedEvent ────

    [Fact]
    public void IdentityUserUpdatedEvent_SetsUserId()
    {
        var evt = new IdentityUserUpdatedEvent("user-1");

        evt.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void IdentityUserUpdatedEvent_SupportsRecordEquality()
    {
        var evt1 = new IdentityUserUpdatedEvent("user-1");
        var evt2 = new IdentityUserUpdatedEvent("user-1");

        evt1.ShouldBe(evt2);
    }

    // ──── IdentityUserDeletedEvent ────

    [Fact]
    public void IdentityUserDeletedEvent_SetsUserId()
    {
        var evt = new IdentityUserDeletedEvent("user-1");

        evt.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void IdentityUserDeletedEvent_TenantIdDefaultsToNull()
    {
        var evt = new IdentityUserDeletedEvent("user-1");

        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void IdentityUserDeletedEvent_AcceptsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var evt = new IdentityUserDeletedEvent("user-1", tenantId);

        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void IdentityUserDeletedEvent_SupportsRecordEquality()
    {
        var tenantId = Guid.NewGuid();
        var evt1 = new IdentityUserDeletedEvent("user-1", tenantId);
        var evt2 = new IdentityUserDeletedEvent("user-1", tenantId);

        evt1.ShouldBe(evt2);
    }

    // ──── UserCacheEntryErasedEvent ────

    [Fact]
    public void UserCacheEntryErasedEvent_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var evt = new UserCacheEntryErasedEvent("user-1", tenantId);

        evt.ExternalUserId.ShouldBe("user-1");
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void UserCacheEntryErasedEvent_AllowsNullTenantId()
    {
        var evt = new UserCacheEntryErasedEvent("user-1", null);

        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void UserCacheEntryErasedEvent_ImplementsIDomainEvent()
    {
        var evt = new UserCacheEntryErasedEvent("user-1", null);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }
}
