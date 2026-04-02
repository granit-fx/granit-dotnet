using Granit.Events;
using Granit.Identity.Federated.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Events;

public sealed class UserCacheEntryErasedEventTests
{
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
