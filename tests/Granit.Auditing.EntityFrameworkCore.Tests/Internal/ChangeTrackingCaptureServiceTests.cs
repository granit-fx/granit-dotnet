// =============================================================================
// ChangeTrackingCaptureServiceTests - Change tracking capture and persistence
// =============================================================================
// Verifies (standalone mode — host context does not map the audit entities, so
// CaptureAsync holds a pending entry and OnSavedAsync persists it through the
// real AuditPersistencePipeline into a Sqlite-backed AuditingDbContext):
//   - Capture skips entities in Unchanged/Detached state
//   - Capture skips entities with [AuditIgnore]
//   - Capture records Added, Modified, Deleted entities
//   - Capture detects soft-delete transitions
//   - PropertyTracking disabled skips property snapshots
//   - OnSavedAsync persists the captured entry and dispatches AuditEntryPersistedEto
//   - OnSavedAsync is a no-op when nothing was captured or when called twice
//   - OnSaveFailed drops the staged entry
//   - SerializeValue handles string, Guid, bool, int
//   - Value protection: mask, omit, hash strategies
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Auditing.Attributes;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Events;
using Granit.Auditing.Options;
using Granit.DataProtection;
using Granit.Domain;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class ChangeTrackingCaptureServiceTests : IDisposable
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IIntegrationEventDispatcher _eventDispatcher = Substitute.For<IIntegrationEventDispatcher>();
    private readonly AuditingOptions _options = new() { EnablePropertyTracking = true };
    private readonly IMeterFactory _meterFactory;
    private readonly AuditingMetrics _metrics;
    private readonly SimpleGuidGenerator _guidGenerator = new();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuditingDbContext> _auditDbOptions;
    private readonly DbContext _dbContext;

    public ChangeTrackingCaptureServiceTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero));
        _currentUserService.UserId.Returns("test-user");
        _currentUserService.UserName.Returns("Test User");
        _currentTenant.IsAvailable.Returns(false);

        _meterFactory = new TestMeterFactory();
        _metrics = new AuditingMetrics(_meterFactory);

        // SQLite in-memory with a shared connection — real isolated audit store.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _auditDbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseSqlite(_connection)
            .Options;
        using (AuditingDbContext ctx = new(_auditDbOptions, GranitDesignTime.CurrentTenant))
        {
            ctx.Database.EnsureCreated();
        }

        _dbContext = CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.Count.ShouldBe(1);
        entry.EntityChanges.First().ChangeType.ShouldBe(AuditChangeType.Created);
        entry.EntityChanges.First().EntityType.ShouldBe("TestEntity");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.ShouldHaveSingleItem().ChangeType.ShouldBe(AuditChangeType.Deleted);
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.ShouldHaveSingleItem().ChangeType.ShouldBe(AuditChangeType.Modified);
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert — nothing should be persisted
        (await GetPersistedEntriesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Capture_AuditIgnoredEntity_IsSkipped()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new IgnoredEntity { Id = 1, Secret = "hidden" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        (await GetPersistedEntriesAsync()).ShouldBeEmpty();
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.ShouldHaveSingleItem().ChangeType.ShouldBe(AuditChangeType.SoftDeleted);
    }

    // -------------------------------------------------------------------------
    // Capture — entry metadata
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Capture_SetsUserInfoFromCurrentUserService()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.UserId.ShouldBe("test-user");
        entry.UserName.ShouldBe("Test User");
    }

    [Fact]
    public async Task Capture_WithNoUserId_FallsBackToSystem()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.UserId.ShouldBe("system");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.Timestamp.ShouldBe(fixedTime);
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task Capture_WithTenantUnavailable_SetsNullTenantId()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(false);

        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task Capture_SetsCategoryToDataMutation()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.Category.ShouldBe(AuditCategory.DataMutation);
    }

    // -------------------------------------------------------------------------
    // OnSavedAsync — integration event dispatch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnSavedAsync_StandaloneMode_DispatchesAuditEntryPersistedEto()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        await _eventDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IIntegrationEvent>>(events =>
                events.Count == 1 &&
                events[0] is AuditEntryPersistedEto &&
                ((AuditEntryPersistedEto)events[0]).Id == entry.Id &&
                ((AuditEntryPersistedEto)events[0]).EntityChangeCount == 1),
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.Count.ShouldBeGreaterThan(0);
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Capture_AddedEntity_PropertyChangesHaveNullOriginalValue()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "Brand New" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Name" &&
            p.OriginalValue == null &&
            p.NewValue == "Brand New");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Name" &&
            p.OriginalValue == "ToDelete" &&
            p.NewValue == null);
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p => p.PropertyName == "Name");
        entry.EntityChanges.First().PropertyChanges.ShouldNotContain(p => p.PropertyName == "Description");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Email" && p.NewValue == "***");
    }

    [Fact]
    public async Task Capture_SensitiveDataOmit_ReturnsNull()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new SensitiveEntity { Id = 1, Password = "super-secret" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Password" && p.NewValue == null);
    }

    [Fact]
    public async Task Capture_SensitiveDataHash_ReturnsHashedValue()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new SensitiveEntity { Id = 1, ExternalId = "ext-123" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "ExternalId" &&
            p.NewValue != null &&
            p.NewValue.StartsWith("sha256:", StringComparison.Ordinal));
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p => p.PropertyName == "Name");
        entry.EntityChanges.First().PropertyChanges.ShouldNotContain(p => p.PropertyName == "InternalNotes");
    }

    [Fact]
    public async Task Capture_PropertyWithAuditIgnore_EntityStillCaptured()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new PropertyIgnoredEntity { Id = 1, Name = "Visible", InternalNotes = "Hidden" });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert — entity is captured (only class-level [AuditIgnore] skips entirely)
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.ShouldHaveSingleItem().EntityType.ShouldBe("PropertyIgnoredEntity");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert — Name change is captured, InternalNotes is excluded
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p => p.PropertyName == "Name");
        entry.EntityChanges.First().PropertyChanges.ShouldNotContain(p => p.PropertyName == "InternalNotes");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Second capture with a fresh DbContext entry
        await using DbContext secondContext = CreateInMemoryDbContext();
        secondContext.Add(new PropertyIgnoredEntity { Id = 2, Name = "Second", InternalNotes = "Secret2" });
        await CaptureAndCompleteAsync(service, secondContext);

        // Assert — both captures should exclude InternalNotes
        List<AuditEntry> entries = await GetPersistedEntriesAsync();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(e =>
            !e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "InternalNotes"));
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Name" && p.NewValue == "HelloWorld");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "GuidProp" && p.NewValue == guidValue.ToString());
    }

    [Fact]
    public async Task Capture_BoolProperty_SerializesToString()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TypedEntity { Id = 1, Activated = true });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Activated" && p.NewValue == "True");
    }

    [Fact]
    public async Task Capture_IntProperty_SerializesToString()
    {
        // Arrange
        _options.EnablePropertyTracking = true;
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TypedEntity { Id = 1, IntProp = 42 });

        // Act
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().PropertyChanges.ShouldContain(p =>
            p.PropertyName == "IntProp" && p.NewValue == "42");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.First().EntityId.ShouldBe("42");
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
        await CaptureAndCompleteAsync(service, _dbContext);

        // Assert
        AuditEntry entry = (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
        entry.EntityChanges.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // OnSavedAsync — idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnSavedAsync_WithNothingCaptured_DoesNotPersist()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();

        // Act — no CaptureAsync call
        await service.OnSavedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        (await GetPersistedEntriesAsync()).ShouldBeEmpty();
        await _eventDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnSavedAsync_CalledTwice_OnlyPersistsOnce()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "Once" });
        await service.CaptureAsync(_dbContext, TestContext.Current.CancellationToken);

        // Act
        await service.OnSavedAsync(_dbContext, TestContext.Current.CancellationToken);
        await service.OnSavedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        (await GetPersistedEntriesAsync()).ShouldHaveSingleItem();
    }

    // -------------------------------------------------------------------------
    // OnSaveFailed — staged state is dropped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnSaveFailed_DropsStagedEntry()
    {
        // Arrange
        ChangeTrackingCaptureService service = CreateService();
        _dbContext.Add(new TestEntity { Id = 1, Name = "Doomed" });
        await service.CaptureAsync(_dbContext, TestContext.Current.CancellationToken);

        // Act — host save failed, then a subsequent completion must not persist
        service.OnSaveFailed(_dbContext);
        await service.OnSavedAsync(_dbContext, TestContext.Current.CancellationToken);

        // Assert
        (await GetPersistedEntriesAsync()).ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Null context guards
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CaptureAsync_NullContext_ThrowsArgumentNullException()
    {
        ChangeTrackingCaptureService service = CreateService();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await service.CaptureAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnSavedAsync_NullContext_ThrowsArgumentNullException()
    {
        ChangeTrackingCaptureService service = CreateService();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await service.OnSavedAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OnSaveFailed_NullContext_ThrowsArgumentNullException()
    {
        ChangeTrackingCaptureService service = CreateService();

        Should.Throw<ArgumentNullException>(() => service.OnSaveFailed(null!));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ChangeTrackingCaptureService CreateService()
    {
        AuditPersistencePipeline pipeline = new(
            new TestAuditDbContextFactory(_auditDbOptions),
            _eventDispatcher,
            _guidGenerator,
            _metrics,
            NullLogger<AuditPersistencePipeline>.Instance);

        return new ChangeTrackingCaptureService(
            _clock,
            _currentUserService,
            _currentTenant,
            pipeline,
            _guidGenerator,
            Microsoft.Extensions.Options.Options.Create(_options),
            _metrics,
            httpContextAccessor: null,
            NullLogger<ChangeTrackingCaptureService>.Instance);
    }

    private static async Task CaptureAndCompleteAsync(ChangeTrackingCaptureService service, DbContext context)
    {
        await service.CaptureAsync(context, TestContext.Current.CancellationToken);
        await service.OnSavedAsync(context, TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEntry>> GetPersistedEntriesAsync()
    {
        await using AuditingDbContext ctx = new(_auditDbOptions, GranitDesignTime.CurrentTenant);
        return await ctx.AuditEntries
            .IgnoreQueryFilters()
            .Include(e => e.EntityChanges)
            .ThenInclude(c => c.PropertyChanges)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static TestDbContext CreateInMemoryDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestAuditDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant);
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
        public bool Activated { get; set; }
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
