using Granit.AuditLog.Domain;
using Granit.AuditLog.Endpoints.Dtos;
using Granit.AuditLog.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Internal;

public sealed class AuditLogResponseMapperAdditionalTests
{
    [Fact]
    public void ToSummaryResponse_WithEmptyEntityChanges_ReturnsZeroCount()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
        };

        AuditLogEntryResponse response = AuditLogResponseMapper.ToSummaryResponse(entry);

        response.EntityChangeCount.ShouldBe(0);
    }

    [Fact]
    public void ToSummaryResponse_WithMultipleEntityChanges_CountsCorrectly()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange { EntityType = "Patient", EntityId = "1", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "Address", EntityId = "2", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "Phone", EntityId = "3", ChangeType = AuditChangeType.Created },
            ],
        };

        AuditLogEntryResponse response = AuditLogResponseMapper.ToSummaryResponse(entry);

        response.EntityChangeCount.ShouldBe(3);
    }

    [Fact]
    public void ToSummaryResponse_MapsCategory_AsString()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.ConfigurationChange,
        };

        AuditLogEntryResponse response = AuditLogResponseMapper.ToSummaryResponse(entry);

        response.Category.ShouldBe("ConfigurationChange");
    }

    [Fact]
    public void ToDetailResponse_WithEmptyEntityChanges_ReturnsEmptyList()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
        };

        AuditLogEntryDetailResponse response = AuditLogResponseMapper.ToDetailResponse(entry);

        response.EntityChanges.ShouldBeEmpty();
    }

    [Fact]
    public void ToDetailResponse_WithNullableFields_MapsNulls()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            UserName = null,
            Category = AuditLogCategory.DataMutation,
            IpAddress = null,
            TenantId = null,
            CorrelationId = null,
        };

        AuditLogEntryDetailResponse response = AuditLogResponseMapper.ToDetailResponse(entry);

        response.UserName.ShouldBeNull();
        response.IpAddress.ShouldBeNull();
        response.TenantId.ShouldBeNull();
        response.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void ToDetailResponse_MapsPropertyChangesWithNullValues()
    {
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
                    EntityId = "1",
                    ChangeType = AuditChangeType.Created,
                    PropertyChanges =
                    [
                        new AuditPropertyChange
                        {
                            PropertyName = "Name",
                            OriginalValue = null,
                            NewValue = "New Patient",
                        },
                    ],
                },
            ],
        };

        AuditLogEntryDetailResponse response = AuditLogResponseMapper.ToDetailResponse(entry);

        AuditPropertyChangeResponse propChange = response.EntityChanges[0].PropertyChanges[0];
        propChange.OriginalValue.ShouldBeNull();
        propChange.NewValue.ShouldBe("New Patient");
    }

    [Fact]
    public void ToDetailResponse_MapsAllChangeTypes()
    {
        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange { EntityType = "A", EntityId = "1", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "B", EntityId = "2", ChangeType = AuditChangeType.Modified },
                new AuditEntityChange { EntityType = "C", EntityId = "3", ChangeType = AuditChangeType.Deleted },
                new AuditEntityChange { EntityType = "D", EntityId = "4", ChangeType = AuditChangeType.SoftDeleted },
            ],
        };

        AuditLogEntryDetailResponse response = AuditLogResponseMapper.ToDetailResponse(entry);

        response.EntityChanges[0].ChangeType.ShouldBe("Created");
        response.EntityChanges[1].ChangeType.ShouldBe("Modified");
        response.EntityChanges[2].ChangeType.ShouldBe("Deleted");
        response.EntityChanges[3].ChangeType.ShouldBe("SoftDeleted");
    }
}
