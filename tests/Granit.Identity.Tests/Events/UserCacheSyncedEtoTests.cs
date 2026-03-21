using Granit.Core.Events;
using Granit.Identity.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Events;

public sealed class UserCacheSyncedEtoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;

        var eto = new UserCacheSyncedEto("user-1", tenantId, syncedAt);

        eto.ExternalUserId.ShouldBe("user-1");
        eto.TenantId.ShouldBe(tenantId);
        eto.SyncedAt.ShouldBe(syncedAt);
    }

    [Fact]
    public void Constructor_AllowsNullTenantId()
    {
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;

        var eto = new UserCacheSyncedEto("user-1", null, syncedAt);

        eto.TenantId.ShouldBeNull();
    }

    [Fact]
    public void ImplementsIIntegrationEvent()
    {
        var eto = new UserCacheSyncedEto("user-1", null, DateTimeOffset.UtcNow);

        eto.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();

        var eto1 = new UserCacheSyncedEto("user-1", tenantId, syncedAt);
        var eto2 = new UserCacheSyncedEto("user-1", tenantId, syncedAt);

        eto1.ShouldBe(eto2);
    }

    [Fact]
    public void Equality_DifferentUserId_AreNotEqual()
    {
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;

        var eto1 = new UserCacheSyncedEto("user-1", null, syncedAt);
        var eto2 = new UserCacheSyncedEto("user-2", null, syncedAt);

        eto1.ShouldNotBe(eto2);
    }
}
