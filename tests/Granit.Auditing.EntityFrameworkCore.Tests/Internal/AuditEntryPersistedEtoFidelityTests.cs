// =============================================================================
// AuditEntryPersistedEtoFidelityTests - the Eto always carries the persisted Id
// =============================================================================
// Verifies the AuditPersistencePipeline invariant that AuditEntryPersistedEto.Id
// equals the AuditEntry.Id actually written to the database, on BOTH paths:
//   - Embedded: the Eto dispatched pre-commit by StageAsync matches the entry
//     the host transaction persisted (Id, Category, EntityChangeCount, TenantId).
//   - Standalone: PersistAsync populates an empty Id eagerly, saves the row
//     under it, and dispatches the Eto with exactly that Id; a preset Id is kept.
//   - No Eto without a row: when the standalone save fails, PersistAsync throws
//     and the dispatcher is never invoked.
// =============================================================================

using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Events;
using Granit.Events;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class AuditEntryPersistedEtoFidelityTests
{
    // -------------------------------------------------------------------------
    // Embedded path — Eto captured from a real interceptor-driven save
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbeddedSave_DispatchedEto_MatchesThePersistedAuditEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using EmbeddedAuditingTestHarness harness = new(tenantId);

        List<AuditEntryPersistedEto> dispatched = [];
        harness.EventDispatcher
            .When(d => d.DispatchAsync(
                Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
                Arg.Any<CancellationToken>()))
            .Do(call => dispatched.AddRange(
                call.Arg<IReadOnlyList<IIntegrationEvent>>().OfType<AuditEntryPersistedEto>()));

        await using EmbeddedHostDbContext context = harness.CreateAuditedHostContext();
        context.Customers.Add(new HostCustomer { Id = 1, Name = "Alice" });

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — query the persisted entry back and compare against the Eto.
        AuditEntry persisted = (await harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
        AuditEntryPersistedEto eto = dispatched.ShouldHaveSingleItem();
        eto.Id.ShouldBe(persisted.Id);
        eto.Category.ShouldBe(persisted.Category);
        eto.EntityChangeCount.ShouldBe(persisted.EntityChanges.Count);
        eto.EntityChangeCount.ShouldBe(1);
        eto.TenantId.ShouldBe(tenantId);
        persisted.TenantId.ShouldBe(tenantId);
    }

    // -------------------------------------------------------------------------
    // Standalone path — PersistAsync id handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PersistAsync_WithEmptyId_PopulatesId_RowAndEtoCarryIt()
    {
        // Arrange
        await using SqliteConnection connection = await CreateAuditStoreAsync();
        DbContextOptions<AuditingDbContext> dbOptions = OptionsFor(connection);
        IIntegrationEventDispatcher dispatcher = Substitute.For<IIntegrationEventDispatcher>();
        List<AuditEntryPersistedEto> dispatched = CaptureDispatched(dispatcher);
        AuditPersistencePipeline pipeline = CreatePipeline(dbOptions, dispatcher);

        AuditEntry entry = CreateEntry(id: Guid.Empty);
        entry.Id.ShouldBe(Guid.Empty);

        // Act
        AuditEntry result = await pipeline.PersistAsync(entry, TestContext.Current.CancellationToken);

        // Assert — id populated, row exists under it, Eto carries exactly that id.
        result.ShouldBeSameAs(entry);
        result.Id.ShouldNotBe(Guid.Empty);

        await using AuditingDbContext verify = new(dbOptions, GranitDesignTime.CurrentTenant);
        AuditEntry? persisted = await verify.AuditEntries
            .IgnoreQueryFilters()
            .Include(e => e.EntityChanges)
            .SingleOrDefaultAsync(e => e.Id == result.Id, TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.EntityChanges.ShouldHaveSingleItem();

        AuditEntryPersistedEto eto = dispatched.ShouldHaveSingleItem();
        eto.Id.ShouldBe(result.Id);
        eto.EntityChangeCount.ShouldBe(1);
    }

    [Fact]
    public async Task PersistAsync_WithPresetId_KeepsIt()
    {
        // Arrange
        await using SqliteConnection connection = await CreateAuditStoreAsync();
        DbContextOptions<AuditingDbContext> dbOptions = OptionsFor(connection);
        IIntegrationEventDispatcher dispatcher = Substitute.For<IIntegrationEventDispatcher>();
        List<AuditEntryPersistedEto> dispatched = CaptureDispatched(dispatcher);
        AuditPersistencePipeline pipeline = CreatePipeline(dbOptions, dispatcher);

        var presetId = Guid.NewGuid();
        AuditEntry entry = CreateEntry(presetId);

        // Act
        AuditEntry result = await pipeline.PersistAsync(entry, TestContext.Current.CancellationToken);

        // Assert
        result.Id.ShouldBe(presetId);
        await using AuditingDbContext verify = new(dbOptions, GranitDesignTime.CurrentTenant);
        (await verify.AuditEntries.IgnoreQueryFilters()
            .AnyAsync(e => e.Id == presetId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        dispatched.ShouldHaveSingleItem().Id.ShouldBe(presetId);
    }

    // -------------------------------------------------------------------------
    // Standalone path — no Eto for a row that failed to persist
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PersistAsync_WhenSaveFails_Throws_AndDispatchesNoEto()
    {
        // Arrange — the target database has NO schema (EnsureCreated deliberately
        // skipped), so the standalone save fails at the provider level.
        await using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        DbContextOptions<AuditingDbContext> dbOptions = OptionsFor(connection);
        IIntegrationEventDispatcher dispatcher = Substitute.For<IIntegrationEventDispatcher>();
        AuditPersistencePipeline pipeline = CreatePipeline(dbOptions, dispatcher);

        AuditEntry entry = CreateEntry(Guid.NewGuid());

        // Act & Assert — the save throws and the event never leaves the pipeline:
        // the row is the source of truth, no Eto without a persisted row.
        await Should.ThrowAsync<DbUpdateException>(
            () => pipeline.PersistAsync(entry, TestContext.Current.CancellationToken));

        await dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<SqliteConnection> CreateAuditStoreAsync()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using AuditingDbContext context = new(OptionsFor(connection), GranitDesignTime.CurrentTenant);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static DbContextOptions<AuditingDbContext> OptionsFor(SqliteConnection connection) =>
        new DbContextOptionsBuilder<AuditingDbContext>()
            .UseSqlite(connection)
            .Options;

    private static AuditPersistencePipeline CreatePipeline(
        DbContextOptions<AuditingDbContext> dbOptions,
        IIntegrationEventDispatcher dispatcher)
    {
        RecordingMeterFactory meterFactory = new();
        return new AuditPersistencePipeline(
            new StubAuditingDbContextFactory(dbOptions),
            dispatcher,
            new Granit.Guids.SimpleGuidGenerator(),
            new AuditingMetrics(meterFactory),
            NullLogger<AuditPersistencePipeline>.Instance);
    }

    private static List<AuditEntryPersistedEto> CaptureDispatched(IIntegrationEventDispatcher dispatcher)
    {
        List<AuditEntryPersistedEto> dispatched = [];
        dispatcher
            .When(d => d.DispatchAsync(
                Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
                Arg.Any<CancellationToken>()))
            .Do(call => dispatched.AddRange(
                call.Arg<IReadOnlyList<IIntegrationEvent>>().OfType<AuditEntryPersistedEto>()));
        return dispatched;
    }

    private static AuditEntry CreateEntry(Guid id)
    {
        var changeId = Guid.NewGuid();
        return new AuditEntry
        {
            Id = id,
            Timestamp = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            UserId = "fidelity-user",
            Category = AuditCategory.DataMutation,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            CreatedBy = "fidelity-user",
            EntityChanges =
            [
                new AuditEntityChange
                {
                    Id = changeId,
                    AuditEntryId = id,
                    EntityType = "Customer",
                    EntityId = "1",
                    ChangeType = AuditChangeType.Created,
                },
            ],
        };
    }
}
