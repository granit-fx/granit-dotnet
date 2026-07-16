using Granit.Identity.Local.Events;
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
        var evt = new UserRegisteredEto(userId, tenantId);

        evt.UserId.ShouldBe(userId);
        evt.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void UserRegisteredEto_AllowsNullTenant()
    {
        var evt = new UserRegisteredEto(Guid.NewGuid(), null);
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
}
