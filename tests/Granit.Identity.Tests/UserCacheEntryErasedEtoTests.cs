using Granit.Core.Events;
using Granit.Identity.Events;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class UserCacheEntryErasedEtoTests
{
    [Fact]
    public void ImplementsIIntegrationEvent() =>
        new UserCacheEntryErasedEto("ext-user-1", Guid.NewGuid(), DateTimeOffset.UtcNow)
            .ShouldBeAssignableTo<IIntegrationEvent>();

    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset erasedAt = DateTimeOffset.UtcNow;
        var evt = new UserCacheEntryErasedEto("ext-user-1", tenantId, erasedAt);

        evt.ExternalUserId.ShouldBe("ext-user-1");
        evt.TenantId.ShouldBe(tenantId);
        evt.ErasedAt.ShouldBe(erasedAt);
    }

    [Fact]
    public void Properties_AllowNullTenantId()
    {
        DateTimeOffset erasedAt = DateTimeOffset.UtcNow;
        var evt = new UserCacheEntryErasedEto("ext-user-1", null, erasedAt);

        evt.TenantId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset erasedAt = DateTimeOffset.UtcNow;
        var evt1 = new UserCacheEntryErasedEto("ext-user-1", tenantId, erasedAt);
        var evt2 = new UserCacheEntryErasedEto("ext-user-1", tenantId, erasedAt);

        evt1.ShouldBe(evt2);
    }
}
