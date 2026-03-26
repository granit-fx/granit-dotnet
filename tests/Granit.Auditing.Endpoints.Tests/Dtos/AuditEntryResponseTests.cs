using Granit.Auditing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Dtos;

public sealed class AuditEntryResponseTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var id = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();

        AuditEntryResponse response = new(
            id,
            timestamp,
            "user-1",
            "Jane Doe",
            "DataMutation",
            tenantId,
            "trace-abc",
            3);

        response.Id.ShouldBe(id);
        response.Timestamp.ShouldBe(timestamp);
        response.UserId.ShouldBe("user-1");
        response.UserName.ShouldBe("Jane Doe");
        response.Category.ShouldBe("DataMutation");
        response.TenantId.ShouldBe(tenantId);
        response.CorrelationId.ShouldBe("trace-abc");
        response.EntityChangeCount.ShouldBe(3);
    }

    [Fact]
    public void WithNullableFields_AcceptsNull()
    {
        AuditEntryResponse response = new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "user-1",
            null,
            "DataMutation",
            null,
            null,
            0);

        response.UserName.ShouldBeNull();
        response.TenantId.ShouldBeNull();
        response.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        DateTimeOffset ts = DateTimeOffset.UtcNow;

        AuditEntryResponse r1 = new(id, ts, "u1", null, "DataMutation", null, null, 1);
        AuditEntryResponse r2 = new(id, ts, "u1", null, "DataMutation", null, null, 1);

        r1.ShouldBe(r2);
    }
}
