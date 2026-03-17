using Granit.AuditLog.Domain;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditLogEntryTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Act
        AuditLogEntry entry = new()
        {
            Id = id,
            Timestamp = timestamp,
            UserId = "user-123",
            UserName = "John Doe",
            Category = AuditLogCategory.DataMutation,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            TenantId = tenantId,
            CorrelationId = "trace-abc",
        };

        // Assert
        entry.Id.ShouldBe(id);
        entry.Timestamp.ShouldBe(timestamp);
        entry.UserId.ShouldBe("user-123");
        entry.UserName.ShouldBe("John Doe");
        entry.Category.ShouldBe(AuditLogCategory.DataMutation);
        entry.IpAddress.ShouldBe("192.168.1.1");
        entry.UserAgent.ShouldBe("Mozilla/5.0");
        entry.TenantId.ShouldBe(tenantId);
        entry.CorrelationId.ShouldBe("trace-abc");
        entry.EntityChanges.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        AuditLogEntry entry = new();

        // Assert
        entry.Id.ShouldBe(Guid.Empty);
        entry.Timestamp.ShouldBe(default);
        entry.UserId.ShouldBeEmpty();
        entry.UserName.ShouldBeNull();
        entry.Category.ShouldBe(AuditLogCategory.DataMutation);
        entry.IpAddress.ShouldBeNull();
        entry.UserAgent.ShouldBeNull();
        entry.TenantId.ShouldBeNull();
        entry.CorrelationId.ShouldBeNull();
        entry.EntityChanges.ShouldBeEmpty();
    }
}
