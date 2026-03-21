using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Granit.AuditLog.Messages;
using Granit.Guids;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Internal;

public sealed class AuditLogBatchMapperAdditionalTests
{
    [Fact]
    public void ToEntity_SetsCreatedAtFromTimestamp()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        DateTimeOffset timestamp = new(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        AuditLogBatch batch = CreateBatch(timestamp: timestamp);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        entry.CreatedAt.ShouldBe(timestamp);
    }

    [Fact]
    public void ToEntity_SetsCreatedByFromUserId()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        AuditLogBatch batch = CreateBatch(userId: "admin-user");

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        entry.CreatedBy.ShouldBe("admin-user");
    }

    [Fact]
    public void ToEntity_AssignsUniqueIds_ToAllEntities()
    {
        int callCount = 0;
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ =>
        {
            callCount++;
            return Guid.NewGuid();
        });

        AuditLogBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "user-1",
            UserName: null,
            Category: AuditLogCategory.DataMutation,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges:
            [
                new AuditEntityChangeSnapshot("Patient", "1", AuditChangeType.Created,
                [
                    new AuditPropertyChangeSnapshot("Name", null, "John"),
                ]),
            ]);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        // Should create: 1 entry ID + 1 entity change ID + 1 property change ID = 3
        callCount.ShouldBe(3);
        entry.Id.ShouldNotBe(Guid.Empty);
        entry.EntityChanges.First().Id.ShouldNotBe(Guid.Empty);
        entry.EntityChanges.First().PropertyChanges.First().Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ToEntity_SetsForeignKeys_Correctly()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        AuditLogBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "user-1",
            UserName: null,
            Category: AuditLogCategory.DataMutation,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges:
            [
                new AuditEntityChangeSnapshot("Patient", "1", AuditChangeType.Modified,
                [
                    new AuditPropertyChangeSnapshot("Email", "a@b.com", "c@d.com"),
                ]),
            ]);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        AuditEntityChange entityChange = entry.EntityChanges.First();
        entityChange.AuditLogEntryId.ShouldBe(entry.Id);

        AuditPropertyChange propChange = entityChange.PropertyChanges.First();
        propChange.AuditEntityChangeId.ShouldBe(entityChange.Id);
    }

    [Fact]
    public void ToEntity_WithNullOptionalFields_MapsNulls()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(Guid.NewGuid());

        AuditLogBatch batch = CreateBatch(
            userName: null,
            ipAddress: null,
            userAgent: null,
            tenantId: null,
            correlationId: null);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        entry.UserName.ShouldBeNull();
        entry.IpAddress.ShouldBeNull();
        entry.UserAgent.ShouldBeNull();
        entry.TenantId.ShouldBeNull();
        entry.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void ToEntity_WithMultipleEntityChanges_MapsAll()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        AuditLogBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "user-1",
            UserName: null,
            Category: AuditLogCategory.DataMutation,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges:
            [
                new AuditEntityChangeSnapshot("Patient", "1", AuditChangeType.Created, []),
                new AuditEntityChangeSnapshot("Address", "2", AuditChangeType.Created, []),
                new AuditEntityChangeSnapshot("Phone", "3", AuditChangeType.Created, []),
            ]);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);

        entry.EntityChanges.Count.ShouldBe(3);
    }

    private static AuditLogBatch CreateBatch(
        DateTimeOffset? timestamp = null,
        string userId = "user-1",
        string? userName = "Test",
        string? ipAddress = "10.0.0.1",
        string? userAgent = "Agent",
        Guid? tenantId = null,
        string? correlationId = null) =>
        new(
            Timestamp: timestamp ?? DateTimeOffset.UtcNow,
            UserId: userId,
            UserName: userName,
            Category: AuditLogCategory.DataMutation,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            TenantId: tenantId,
            CorrelationId: correlationId,
            EntityChanges: []);
}
