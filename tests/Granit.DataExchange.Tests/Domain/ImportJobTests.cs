using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Reporting;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Domain;

public sealed class ImportJobTests
{
    [Fact]
    public void ImportJob_inherits_AuditedAggregateRoot() =>
        typeof(ImportJob).IsAssignableTo(typeof(AuditedAggregateRoot)).ShouldBeTrue();

    [Fact]
    public void Create_sets_status_to_Created()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv");

        job.Status.ShouldBe(ImportJobStatus.Created);
    }

    [Fact]
    public void ImportJobStatus_has_all_lifecycle_states() =>
        // Assert — 8 states covering the full lifecycle
        Enum.GetValues<ImportJobStatus>().Length.ShouldBe(8);

    [Fact]
    public void Create_sets_optional_tenant()
    {
        var tenantId = Guid.NewGuid();

        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv",
            tenantId);

        job.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Create_without_tenant_has_null_tenantId()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv");

        job.TenantId.ShouldBeNull();
    }

    [Fact]
    public void SetMappings_stores_typed_collection()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv");
        ImportColumnMapping[] mappings = [new("A", null, MappingConfidence.Manual)];

        job.SetMappings(mappings);

        job.Mappings.ShouldBe(mappings);
    }

    [Fact]
    public void MarkAsExecuting_transitions_status()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv");
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);

        job.MarkAsExecuting();

        job.Status.ShouldBe(ImportJobStatus.Executing);
    }

    [Fact]
    public void Complete_sets_final_status_and_report()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(),
            "Test",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "blob/test.csv");
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();

        ImportReport report = new()
        {
            TotalRows = 100,
            SucceededRows = 100,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 100,
            UpdatedRows = 0,
            Duration = TimeSpan.Zero,
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        };
        job.Complete(ImportJobStatus.Completed, report, completedAt);

        job.Status.ShouldBe(ImportJobStatus.Completed);
        job.Report.ShouldBe(report);
        job.CompletedAt.ShouldBe(completedAt);
    }
}
