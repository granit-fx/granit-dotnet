using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Dtos;
using Granit.Auditing.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Internal;

public sealed class AuditingResponseMapperAdditionalTests
{
    [Fact]
    public void ToSummaryResponse_WithEmptyEntityChanges_ReturnsZeroCount()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
        };

        AuditEntryResponse response = AuditingResponseMapper.ToSummaryResponse(entry);

        response.EntityChangeCount.ShouldBe(0);
    }

    [Fact]
    public void ToSummaryResponse_WithMultipleEntityChanges_CountsCorrectly()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange { EntityType = "Patient", EntityId = "1", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "Address", EntityId = "2", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "Phone", EntityId = "3", ChangeType = AuditChangeType.Created },
            ],
        };

        AuditEntryResponse response = AuditingResponseMapper.ToSummaryResponse(entry);

        response.EntityChangeCount.ShouldBe(3);
    }

    [Fact]
    public void ToSummaryResponse_MapsCategory_AsString()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.ConfigurationChange,
        };

        AuditEntryResponse response = AuditingResponseMapper.ToSummaryResponse(entry);

        response.Category.ShouldBe("ConfigurationChange");
    }

    [Fact]
    public void ToDetailResponse_WithEmptyEntityChanges_ReturnsEmptyList()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
        };

        AuditEntryDetailResponse response = AuditingResponseMapper.ToDetailResponse(entry);

        response.EntityChanges.ShouldBeEmpty();
    }

    [Fact]
    public void ToDetailResponse_WithNullableFields_MapsNulls()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            UserName = null,
            Category = AuditCategory.DataMutation,
            IpAddress = null,
            TenantId = null,
            CorrelationId = null,
        };

        AuditEntryDetailResponse response = AuditingResponseMapper.ToDetailResponse(entry);

        response.UserName.ShouldBeNull();
        response.IpAddress.ShouldBeNull();
        response.TenantId.ShouldBeNull();
        response.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void ToDetailResponse_MapsPropertyChangesWithNullValues()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
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

        AuditEntryDetailResponse response = AuditingResponseMapper.ToDetailResponse(entry);

        AuditPropertyChangeResponse propChange = response.EntityChanges[0].PropertyChanges[0];
        propChange.OriginalValue.ShouldBeNull();
        propChange.NewValue.ShouldBe("New Patient");
    }

    [Fact]
    public void ToDetailResponse_MapsAllChangeTypes()
    {
        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange { EntityType = "A", EntityId = "1", ChangeType = AuditChangeType.Created },
                new AuditEntityChange { EntityType = "B", EntityId = "2", ChangeType = AuditChangeType.Modified },
                new AuditEntityChange { EntityType = "C", EntityId = "3", ChangeType = AuditChangeType.Deleted },
                new AuditEntityChange { EntityType = "D", EntityId = "4", ChangeType = AuditChangeType.SoftDeleted },
            ],
        };

        AuditEntryDetailResponse response = AuditingResponseMapper.ToDetailResponse(entry);

        response.EntityChanges[0].ChangeType.ShouldBe("Created");
        response.EntityChanges[1].ChangeType.ShouldBe("Modified");
        response.EntityChanges[2].ChangeType.ShouldBe("Deleted");
        response.EntityChanges[3].ChangeType.ShouldBe("SoftDeleted");
    }
}
