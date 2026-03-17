using Granit.AuditLog.Domain;
using Granit.AuditLog.Messages;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditLogBatchTests
{
    [Fact]
    public void AuditLogBatch_CanBeCreated()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        AuditPropertyChangeSnapshot propChange = new("Email", "old@test.com", "new@test.com");
        AuditEntityChangeSnapshot entityChange = new("Patient", "123", AuditChangeType.Modified, [propChange]);

        // Act
        AuditLogBatch batch = new(
            Timestamp: timestamp,
            UserId: "user-1",
            UserName: "Test User",
            Category: AuditLogCategory.DataMutation,
            IpAddress: "10.0.0.1",
            UserAgent: "TestAgent",
            TenantId: tenantId,
            CorrelationId: "trace-xyz",
            EntityChanges: [entityChange]);

        // Assert
        batch.Timestamp.ShouldBe(timestamp);
        batch.UserId.ShouldBe("user-1");
        batch.UserName.ShouldBe("Test User");
        batch.Category.ShouldBe(AuditLogCategory.DataMutation);
        batch.TenantId.ShouldBe(tenantId);
        batch.EntityChanges.ShouldHaveSingleItem();
        batch.EntityChanges[0].PropertyChanges.ShouldHaveSingleItem();
        batch.EntityChanges[0].PropertyChanges[0].PropertyName.ShouldBe("Email");
    }
}
