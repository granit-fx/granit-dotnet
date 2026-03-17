using Granit.AuditLog.Domain;
using Granit.AuditLog.Endpoints.Dtos;
using Granit.AuditLog.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Internal;

public sealed class AuditLogResponseMapperTests
{
    [Fact]
    public void ToSummaryResponse_MapsAllFields()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            UserName = "Test",
            Category = AuditLogCategory.DataMutation,
            IpAddress = "10.0.0.1",
            TenantId = tenantId,
            CorrelationId = "trace-1",
            EntityChanges =
            [
                new AuditEntityChange
                {
                    EntityType = "Patient",
                    EntityId = "42",
                    ChangeType = AuditChangeType.Created,
                },
            ],
        };

        // Act
        AuditLogEntryResponse response = AuditLogResponseMapper.ToSummaryResponse(entry);

        // Assert
        response.Id.ShouldBe(entry.Id);
        response.UserId.ShouldBe("user-1");
        response.Category.ShouldBe("DataMutation");
        response.EntityChangeCount.ShouldBe(1);
        response.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void ToDetailResponse_MapsNestedHierarchy()
    {
        // Arrange
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange
                {
                    EntityType = "Patient",
                    EntityId = "42",
                    ChangeType = AuditChangeType.Modified,
                    PropertyChanges =
                    [
                        new AuditPropertyChange
                        {
                            PropertyName = "Email",
                            OriginalValue = "old@test.com",
                            NewValue = "new@test.com",
                        },
                    ],
                },
            ],
        };

        // Act
        AuditLogEntryDetailResponse response = AuditLogResponseMapper.ToDetailResponse(entry);

        // Assert
        response.EntityChanges.ShouldHaveSingleItem();
        AuditEntityChangeResponse entityChange = response.EntityChanges[0];
        entityChange.EntityType.ShouldBe("Patient");
        entityChange.ChangeType.ShouldBe("Modified");
        entityChange.PropertyChanges.ShouldHaveSingleItem();
        entityChange.PropertyChanges[0].PropertyName.ShouldBe("Email");
        entityChange.PropertyChanges[0].OriginalValue.ShouldBe("old@test.com");
        entityChange.PropertyChanges[0].NewValue.ShouldBe("new@test.com");
    }
}
