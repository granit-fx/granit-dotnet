// =============================================================================
// Tests - PersistenceActivitySource
// =============================================================================
// Verifie que l'ActivitySource est correctement configure avec le bon nom,
// que les constantes d'operation existent, et que les activites sont creees
// quand un listener est attache.
// =============================================================================

using System.Diagnostics;
using Granit.Persistence.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.Diagnostics;

public sealed class PersistenceActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public PersistenceActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PersistenceActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    // ──── Name & Source ────

    [Fact]
    public void Name_IsGranitPersistence() =>
        PersistenceActivitySource.Name.ShouldBe("Granit.Persistence");

    [Fact]
    public void Source_IsNotNull() =>
        PersistenceActivitySource.Source.ShouldNotBeNull();

    // ──── Operation name constants ────

    [Fact]
    public void SaveChangesAudit_IsNotNullOrEmpty() =>
        PersistenceActivitySource.SaveChangesAudit.ShouldNotBeNullOrEmpty();

    [Fact]
    public void SoftDelete_IsNotNullOrEmpty() =>
        PersistenceActivitySource.SoftDelete.ShouldNotBeNullOrEmpty();

    [Fact]
    public void DomainEventDispatch_IsNotNullOrEmpty() =>
        PersistenceActivitySource.DomainEventDispatch.ShouldNotBeNullOrEmpty();

    [Fact]
    public void DataSeed_IsNotNullOrEmpty() =>
        PersistenceActivitySource.DataSeed.ShouldNotBeNullOrEmpty();

    [Fact]
    public void PurgeSoftDeleted_IsNotNullOrEmpty() =>
        PersistenceActivitySource.PurgeSoftDeleted.ShouldNotBeNullOrEmpty();

    // ──── Activity creation ────

    [Fact]
    public void StartActivity_SaveChangesAudit_ReturnsActivityWhenListenerAttached()
    {
        using Activity? activity = PersistenceActivitySource.Source.StartActivity(
            PersistenceActivitySource.SaveChangesAudit);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("persistence.save-changes-audit");
    }

    [Fact]
    public void StartActivity_SoftDelete_ReturnsActivityWhenListenerAttached()
    {
        using Activity? activity = PersistenceActivitySource.Source.StartActivity(
            PersistenceActivitySource.SoftDelete);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("persistence.soft-delete");
    }

    [Fact]
    public void StartActivity_DomainEventDispatch_ReturnsActivityWhenListenerAttached()
    {
        using Activity? activity = PersistenceActivitySource.Source.StartActivity(
            PersistenceActivitySource.DomainEventDispatch);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("persistence.domain-event-dispatch");
    }

    [Fact]
    public void StartActivity_DataSeed_ReturnsActivityWhenListenerAttached()
    {
        using Activity? activity = PersistenceActivitySource.Source.StartActivity(
            PersistenceActivitySource.DataSeed);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("persistence.data-seed");
    }

    [Fact]
    public void StartActivity_PurgeSoftDeleted_ReturnsActivityWhenListenerAttached()
    {
        using Activity? activity = PersistenceActivitySource.Source.StartActivity(
            PersistenceActivitySource.PurgeSoftDeleted);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("persistence.purge-soft-deleted");
    }
}
