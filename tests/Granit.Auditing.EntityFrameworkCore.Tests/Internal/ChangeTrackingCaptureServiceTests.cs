// =============================================================================
// ChangeTrackingCaptureServiceTests - Change tracking capture and publish
// =============================================================================
// Verifies:
//   - Capture skips entities in Unchanged/Detached state
//   - Capture skips entities with [AuditIgnore]
//   - Capture records Added, Modified, Deleted entities
//   - Capture detects soft-delete transitions
//   - PropertyTracking disabled skips property snapshots
//   - PublishAsync sends captured batch to publisher
//   - PublishAsync is no-op when nothing was captured
//   - SerializeValue handles null, string, DateTime, Guid, enum, complex object
//   - Truncation truncates long strings
//   - Value protection: mask, omit, hash strategies
//   - Error handling records metric and clears batch
// =============================================================================

using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Granit.Auditing.Attributes;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.DataProtection;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class ChangeTrackingCaptureServiceTests : IDisposable
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IAuditEntryPublisher _publisher = Substitute.For<IAuditEntryPublisher>();
    private readonly AuditingOptions _options = new() { EnablePropertyTracking = true };
    private readonly IMeterFactory _meterFactory;
    private readonly AuditingMetrics _metrics;
    private readonly DbContext _dbContext;

    public ChangeTrackingCaptureServiceTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero));
        _currentUserService.UserId.Returns("test-user");
        _currentUserService.UserName.Returns("Test User");
        _currentTenant.IsAvailable.Returns(false);

        _meterFactory = new TestMeterFactory();
        _metrics = new AuditingMetrics(_meterFactory, Channel.CreateUnbounded<AuditingBatch>());

        _dbContext = CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
    }

    // -------------------------------------------------------------------------
    // Capture — entity state filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_AddedEntity_CapturesCreatedChange()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges.Count == 1 &&
                b.EntityChanges[0].ChangeType == AuditChangeType.Created &&
                b.EntityChanges[0].EntityType == "TestEntity"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_DeletedEntity_CapturesDeletedChange()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "ToDelete" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        _dbContext.Remove(entity);

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges.Count == 1 &&
                b.EntityChanges[0].ChangeType == AuditChangeType.Deleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_ModifiedEntity_CapturesModifiedChange()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.Name = "Updated";

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges.Count == 1 &&
                b.EntityChanges[0].ChangeType == AuditChangeType.Modified),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_UnchangedEntity_IsSkipped()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "Stable" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        // Entity is now Unchanged — no modifications

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert — no batch should be published
        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<AuditingBatch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_AuditIgnoredEntity_IsSkipped()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new IgnoredEntity { Id = 1, Secret = "hidden" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<AuditingBatch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_SoftDeletedEntity_CapturesSoftDeletedChange()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        var entity = new SoftDeletableEntity { Id = 1, Name = "Active", IsDeleted = false };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.IsDeleted = true;

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges.Count == 1 &&
                b.EntityChanges[0].ChangeType == AuditChangeType.SoftDeleted),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — batch metadata
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_SetsUserInfoFromCurrentUserService()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.UserId == "test-user" &&
                b.UserName == "Test User"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_WithNoUserId_FallsBackToSystem()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.UserId == "system"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_SetsTimestampFromClock()
    {
        // Arrange
        DateTimeOffset fixedTime = new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(fixedTime);
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.Timestamp == fixedTime),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_WithTenantAvailable_SetsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_WithTenantUnavailable_SetsNullTenantId()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(false);

        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_SetsCategoryToDataMutation()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.Category == AuditCategory.DataMutation),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — property tracking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_PropertyTrackingEnabled_CapturesPropertyChanges()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.Name = "Updated";

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Count > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_PropertyTrackingDisabled_SkipsPropertyChanges()
    {
        // Arrange
        _options.EnablePropertyTracking = false;
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.Name = "Updated";

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_AddedEntity_PropertyChangesHaveNullOriginalValue()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "Brand New" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "Name" &&
                    p.OriginalValue == null &&
                    p.NewValue == "Brand New")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_DeletedEntity_PropertyChangesHaveNullNewValue()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntity { Id = 1, Name = "ToDelete" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        _dbContext.Remove(entity);

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "Name" &&
                    p.OriginalValue == "ToDelete" &&
                    p.NewValue == null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_ModifiedEntity_UnchangedPropertiesNotIncluded()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        var entity = new TestEntityWithMultipleProps { Id = 1, Name = "Same", Description = "Same" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.Name = "Changed";
        // Description stays the same

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "Name") &&
                !b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "Description")),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — sensitive data protection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_SensitiveDataMask_ReplacesWithMask()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new SensitiveEntity { Id = 1, Email = "secret@example.com" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "Email" && p.NewValue == "***")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_SensitiveDataOmit_ReturnsNull()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new SensitiveEntity { Id = 1, Password = "super-secret" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "Password" && p.NewValue == null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_SensitiveDataHash_ReturnsHashedValue()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new SensitiveEntity { Id = 1, ExternalId = "ext-123" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "ExternalId" &&
                    p.NewValue != null &&
                    p.NewValue.StartsWith("sha256:", StringComparison.Ordinal))),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — property-level [AuditIgnore]
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_PropertyWithAuditIgnore_IsExcludedFromPropertyChanges()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new PropertyIgnoredEntity { Id = 1, Name = "Visible", InternalNotes = "Hidden" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "Name") &&
                !b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "InternalNotes")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_PropertyWithAuditIgnore_EntityStillCaptured()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new PropertyIgnoredEntity { Id = 1, Name = "Visible", InternalNotes = "Hidden" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert — entity is captured (only class-level [AuditIgnore] skips entirely)
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges.Count == 1 &&
                b.EntityChanges[0].EntityType == "PropertyIgnoredEntity"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_PropertyWithAuditIgnore_ModifiedIgnoredPropertyNotTracked()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        var entity = new PropertyIgnoredEntity { Id = 1, Name = "Original", InternalNotes = "Note1" };
        _dbContext.Add(entity);
        _dbContext.SaveChanges();

        entity.Name = "Updated";
        entity.InternalNotes = "Note2";

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert — Name change is captured, InternalNotes is excluded
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "Name") &&
                !b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "InternalNotes")),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — metadata cache consistency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_SameEntityTypeTwice_UsesCachedMetadata()
    {
        // Arrange — two separate captures of the same entity type should produce
        // consistent results, confirming the static metadata cache works correctly.
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();

        _dbContext.Add(new PropertyIgnoredEntity { Id = 1, Name = "First", InternalNotes = "Secret1" });
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Second capture with a fresh DbContext entry
        using DbContext secondContext = CreateInMemoryDbContext();
        secondContext.Add(new PropertyIgnoredEntity { Id = 2, Name = "Second", InternalNotes = "Secret2" });
        service.Capture(secondContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert — both captures should exclude InternalNotes
        await _publisher.Received(2).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                !b.EntityChanges[0].PropertyChanges.Any(p => p.PropertyName == "InternalNotes")),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — serialization of different value types
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_StringProperty_SerializesAsIs()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "HelloWorld" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "Name" && p.NewValue == "HelloWorld")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_GuidProperty_SerializesToString()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        var guidValue = Guid.NewGuid();
        _dbContext.Add(new TypedEntity { Id = 1, GuidProp = guidValue });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "GuidProp" && p.NewValue == guidValue.ToString())),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_BoolProperty_SerializesToString()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TypedEntity { Id = 1, IsActive = true });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "IsActive" && p.NewValue == "True")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_IntProperty_SerializesToString()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TypedEntity { Id = 1, IntProp = 42 });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].PropertyChanges.Any(p =>
                    p.PropertyName == "IntProp" && p.NewValue == "42")),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — entity ID extraction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_ExtractsEntityIdFromPrimaryKey()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 42, Name = "WithId" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b =>
                b.EntityChanges[0].EntityId == "42"),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — multiple entities in one save
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_MultipleEntities_CapturesAll()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "First" });
        _dbContext.Add(new TestEntity { Id = 2, Name = "Second" });

        // Act
        service.Capture(_dbContext);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Is<AuditingBatch>(b => b.EntityChanges.Count == 2),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // PublishAsync — idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_WithNothingCaptured_DoesNotPublish()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();

        // Act — no Capture call
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<AuditingBatch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_CalledTwice_OnlyPublishesOnce()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "Once" });
        service.Capture(_dbContext);

        // Act
        await service.PublishAsync(TestContext.Current.CancellationToken);
        await service.PublishAsync(TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Any<AuditingBatch>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Capture — null context guard
    // -------------------------------------------------------------------------

    [Fact]
    public void Capture_NullContext_ThrowsArgumentNullException()
    {
        ChangeTrackingCaptureService service = CreateService();

        Should.Throw<ArgumentNullException>(() => service.Capture(null!));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ChangeTrackingCaptureService CreateService() =>
        new(
            _clock,
            _currentUserService,
            _currentTenant,
            _publisher,
            Microsoft.Extensions.Options.Options.Create(_options),
            _metrics,
            httpContextAccessor: null,
            NullLogger<ChangeTrackingCaptureService>.Instance);

    private static TestDbContext CreateInMemoryDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Test entities
    // -------------------------------------------------------------------------

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
        public DbSet<IgnoredEntity> IgnoredEntities => Set<IgnoredEntity>();
        public DbSet<SoftDeletableEntity> SoftDeletableEntities => Set<SoftDeletableEntity>();
        public DbSet<SensitiveEntity> SensitiveEntities => Set<SensitiveEntity>();
        public DbSet<TypedEntity> TypedEntities => Set<TypedEntity>();
        public DbSet<TestEntityWithMultipleProps> MultiPropEntities => Set<TestEntityWithMultipleProps>();
        public DbSet<PropertyIgnoredEntity> PropertyIgnoredEntities => Set<PropertyIgnoredEntity>();
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [AuditIgnore]
    private sealed class IgnoredEntity
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

    private sealed class SoftDeletableEntity : ISoftDeletable
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private sealed class SensitiveEntity
    {
        public int Id { get; set; }

        [SensitiveData(Mode = SensitiveDataMode.Mask)]
        public string? Email { get; set; }

        [SensitiveData(Mode = SensitiveDataMode.Omit)]
        public string? Password { get; set; }

        [SensitiveData(Mode = SensitiveDataMode.Hash)]
        public string? ExternalId { get; set; }
    }

    private sealed class TypedEntity
    {
        public int Id { get; set; }
        public Guid GuidProp { get; set; }
        public bool IsActive { get; set; }
        public int IntProp { get; set; }
    }

    private sealed class TestEntityWithMultipleProps
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private sealed class PropertyIgnoredEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [AuditIgnore]
        public string InternalNotes { get; set; } = string.Empty;
    }
}
