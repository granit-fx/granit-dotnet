// =============================================================================
// AuditPipelineInvariantTests - metric invariants of the persistence choke point
// =============================================================================
// Verifies, with a real AuditingMetrics over a MeterListener (scoped to THIS
// test's meter instances so parallel classes cannot cross-pollute counts):
//   - One embedded save increments granit.auditing.entry.persisted exactly once
//     and records the duration histogram tagged mode=embedded.
//   - One standalone PersistAsync increments the counter exactly once and tags
//     the duration histogram mode=standalone.
//   - An empty batch records nothing and dispatches nothing.
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class AuditPipelineInvariantTests : IDisposable
{
    private const string PersistedCounter = "granit.auditing.entry.persisted";
    private const string DurationHistogram = "granit.auditing.persistence.duration";
    private const string EntityChangesHistogram = "granit.auditing.entry.entity_changes";

    private readonly EmbeddedAuditingTestHarness _harness = new();
    private readonly MeterListener _listener;
    private readonly List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> _longRecordings = [];
    private readonly List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> _doubleRecordings = [];
    private readonly List<(string Name, int Value, KeyValuePair<string, object?>[] Tags)> _intRecordings = [];

    public AuditPipelineInvariantTests()
    {
        _listener = new MeterListener
        {
            // Enable only instruments from THIS harness's meter factory — meters named
            // "Granit.Auditing" are created by other test classes running in parallel.
            InstrumentPublished = (instrument, listener) =>
            {
                if (_harness.MeterFactory.Meters.Contains(instrument.Meter))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
            {
                lock (_longRecordings)
                {
                    _longRecordings.Add((instrument.Name, measurement, tags.ToArray()));
                }
            });
        _listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
            {
                lock (_doubleRecordings)
                {
                    _doubleRecordings.Add((instrument.Name, measurement, tags.ToArray()));
                }
            });
        _listener.SetMeasurementEventCallback<int>(
            (instrument, measurement, tags, _) =>
            {
                lock (_intRecordings)
                {
                    _intRecordings.Add((instrument.Name, measurement, tags.ToArray()));
                }
            });
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _harness.Dispose();
    }

    // -------------------------------------------------------------------------
    // Embedded path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbeddedSave_RecordsPersistedExactlyOnce_WithEmbeddedModeDuration()
    {
        // Arrange
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.Customers.Add(new HostCustomer { Id = 1, Name = "Alice" });

        // Act — one save, one staged entry, one commit.
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — counter incremented exactly once, by exactly 1.
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> persisted =
            [.. _longRecordings.Where(r => r.Name == PersistedCounter)];
        persisted.ShouldHaveSingleItem().Value.ShouldBe(1);
        persisted[0].Tags.ShouldContain(t => t.Key == "tenant_id" && (string?)t.Value == "global");

        // Assert — one duration sample, tagged embedded.
        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> durations =
            [.. _doubleRecordings.Where(r => r.Name == DurationHistogram)];
        durations.ShouldHaveSingleItem().Tags
            .ShouldContain(t => t.Key == "mode" && (string?)t.Value == "embedded");

        // Assert — one entity-change-count sample for the single captured change.
        List<(string Name, int Value, KeyValuePair<string, object?>[] Tags)> changeCounts =
            [.. _intRecordings.Where(r => r.Name == EntityChangesHistogram)];
        changeCounts.ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Standalone path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StandalonePersist_RecordsPersistedExactlyOnce_WithStandaloneModeDuration()
    {
        // Arrange — drive the pipeline directly; its factory targets the harness's
        // (real, schema-created) standalone store.
        AuditPersistencePipeline pipeline =
            _harness.ScopeServices.GetRequiredService<AuditPersistencePipeline>();
        AuditEntry entry = new()
        {
            Timestamp = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            UserId = "invariant-user",
            Category = AuditCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Customer",
                    EntityId = "1",
                    ChangeType = AuditChangeType.Created,
                },
            ],
        };

        // Act
        await pipeline.PersistAsync(entry, TestContext.Current.CancellationToken);

        // Assert — the row landed in the standalone store.
        (await _harness.GetStandaloneAuditEntriesAsync()).ShouldHaveSingleItem();

        // Assert — counter incremented exactly once, duration tagged standalone.
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> persisted =
            [.. _longRecordings.Where(r => r.Name == PersistedCounter)];
        persisted.ShouldHaveSingleItem().Value.ShouldBe(1);

        List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> durations =
            [.. _doubleRecordings.Where(r => r.Name == DurationHistogram)];
        durations.ShouldHaveSingleItem().Tags
            .ShouldContain(t => t.Key == "mode" && (string?)t.Value == "standalone");

        List<(string Name, int Value, KeyValuePair<string, object?>[] Tags)> changeCounts =
            [.. _intRecordings.Where(r => r.Name == EntityChangesHistogram)];
        changeCounts.ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Empty batch — nothing recorded, nothing dispatched
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmptyBatch_RecordsNoMetrics_AndDispatchesNoEto()
    {
        // Arrange — the only change is [AuditIgnore]d, so the capture yields nothing.
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.IgnoredEntities.Add(new AuditIgnoredHostEntity { Id = 1, Payload = "nothing to see" });

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — no persisted counter, no duration, no entity-change histogram, no Eto.
        _longRecordings.ShouldNotContain(r => r.Name == PersistedCounter);
        _doubleRecordings.ShouldNotContain(r => r.Name == DurationHistogram);
        _intRecordings.ShouldNotContain(r => r.Name == EntityChangesHistogram);
        await _harness.EventDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }
}
