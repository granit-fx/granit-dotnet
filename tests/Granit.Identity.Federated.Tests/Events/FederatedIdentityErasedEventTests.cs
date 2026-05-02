using Granit.Events;
using Granit.Identity.Federated.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Events;

public sealed class FederatedIdentityErasedEventTests
{
    [Fact]
    public void FederatedIdentityErasedEvent_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var evt = new FederatedIdentityErasedEvent("user-1", tenantId);

        evt.ExternalUserId.ShouldBe("user-1");
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void FederatedIdentityErasedEvent_AllowsNullTenantId()
    {
        var evt = new FederatedIdentityErasedEvent("user-1", null);

        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void FederatedIdentityErasedEvent_ImplementsIDomainEvent()
    {
        var evt = new FederatedIdentityErasedEvent("user-1", null);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }
}
