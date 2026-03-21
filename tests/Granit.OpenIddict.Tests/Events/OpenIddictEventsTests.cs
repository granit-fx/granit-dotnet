using Granit.OpenIddict.Events;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Events;

public sealed class OpenIddictEventsTests
{
    [Fact]
    public void UserRegisteredEto_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var evt = new UserRegisteredEto(userId, "alice@test.com", tenantId);

        evt.UserId.ShouldBe(userId);
        evt.Email.ShouldBe("alice@test.com");
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void UserRegisteredEto_AllowsNullTenant()
    {
        var evt = new UserRegisteredEto(Guid.NewGuid(), "alice@test.com", null);
        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void AccountDeletedEto_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var evt = new AccountDeletedEto(userId, tenantId);

        evt.UserId.ShouldBe(userId);
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void UserImpersonatedEto_SetsAllProperties()
    {
        var targetId = Guid.NewGuid();
        var impersonatorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var evt = new UserImpersonatedEto(targetId, impersonatorId, tenantId, now);

        evt.TargetUserId.ShouldBe(targetId);
        evt.ImpersonatorId.ShouldBe(impersonatorId);
        evt.TenantId.ShouldBe(tenantId);
        evt.OccurredAt.ShouldBe(now);
    }

    [Fact]
    public void UserImpersonatedEto_ImplementsIIntegrationEvent()
    {
        var evt = new UserImpersonatedEto(Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        evt.ShouldBeAssignableTo<Granit.Core.Events.IIntegrationEvent>();
    }
}
