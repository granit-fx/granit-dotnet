using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Guids;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class AuditingBatchMapperTests
{
    [Fact]
    public void ToEntity_MapsAllFields()
    {
        // Arrange
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();

        AuditingBatch batch = new(
            Timestamp: timestamp,
            UserId: "user-1",
            UserName: "Test User",
            Category: AuditCategory.DataMutation,
            IpAddress: "10.0.0.1",
            UserAgent: "TestAgent",
            TenantId: tenantId,
            CorrelationId: "trace-123",
            EntityChanges:
            [
                new AuditEntityChangeSnapshot(
                    "Patient", "42", AuditChangeType.Modified,
                    [
                        new AuditPropertyChangeSnapshot("Name", "Old", "New"),
                        new AuditPropertyChangeSnapshot("Email", "a@b.com", "c@d.com"),
                    ]),
            ]);

        // Act
        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);

        // Assert
        entry.Timestamp.ShouldBe(timestamp);
        entry.UserId.ShouldBe("user-1");
        entry.UserName.ShouldBe("Test User");
        entry.Category.ShouldBe(AuditCategory.DataMutation);
        entry.IpAddress.ShouldBe("10.0.0.1");
        entry.TenantId.ShouldBe(tenantId);
        entry.CorrelationId.ShouldBe("trace-123");
        entry.EntityChanges.Count.ShouldBe(1);

        AuditEntityChange entityChange = entry.EntityChanges.First();
        entityChange.EntityType.ShouldBe("Patient");
        entityChange.EntityId.ShouldBe("42");
        entityChange.ChangeType.ShouldBe(AuditChangeType.Modified);
        entityChange.PropertyChanges.Count.ShouldBe(2);

        AuditPropertyChange propChange = entityChange.PropertyChanges.First();
        propChange.PropertyName.ShouldBe("Name");
        propChange.OriginalValue.ShouldBe("Old");
        propChange.NewValue.ShouldBe("New");
    }

    [Fact]
    public void ToEntity_WithEmptyChanges_ReturnsEntryWithNoChildren()
    {
        // Arrange
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(Guid.NewGuid());

        AuditingBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "system",
            UserName: null,
            Category: AuditCategory.ConfigurationChange,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges: []);

        // Act
        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);

        // Assert
        entry.EntityChanges.ShouldBeEmpty();
        entry.Category.ShouldBe(AuditCategory.ConfigurationChange);
    }
}
