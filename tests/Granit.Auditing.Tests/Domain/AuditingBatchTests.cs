using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditingBatchTests
{
    [Fact]
    public void AuditingBatch_CanBeCreated()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        AuditPropertyChangeSnapshot propChange = new("Email", "old@test.com", "new@test.com");
        AuditEntityChangeSnapshot entityChange = new("Patient", "123", AuditChangeType.Modified, [propChange]);

        // Act
        AuditingBatch batch = new(
            Timestamp: timestamp,
            UserId: "user-1",
            UserName: "Test User",
            Category: AuditCategory.DataMutation,
            IpAddress: "10.0.0.1",
            UserAgent: "TestAgent",
            TenantId: tenantId,
            CorrelationId: "trace-xyz",
            EntityChanges: [entityChange]);

        // Assert
        batch.Timestamp.ShouldBe(timestamp);
        batch.UserId.ShouldBe("user-1");
        batch.UserName.ShouldBe("Test User");
        batch.Category.ShouldBe(AuditCategory.DataMutation);
        batch.TenantId.ShouldBe(tenantId);
        batch.EntityChanges.ShouldHaveSingleItem();
        batch.EntityChanges[0].PropertyChanges.ShouldHaveSingleItem();
        batch.EntityChanges[0].PropertyChanges[0].PropertyName.ShouldBe("Email");
    }
}
