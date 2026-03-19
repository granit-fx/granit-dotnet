using Granit.Core.Domain;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportJobTests
{
    [Fact]
    public void Inherits_AuditedAggregateRoot() =>
        typeof(ExportJob).IsAssignableTo(typeof(AuditedAggregateRoot)).ShouldBeTrue();

    [Fact]
    public void Create_DefaultStatus_IsQueued()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(),
            "Test",
            "xlsx",
            "{}");

        job.Status.ShouldBe(ExportJobStatus.Queued);
    }

    [Fact]
    public void Create_NullableProperties_AreNullByDefault()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(),
            "Test",
            "csv",
            "{}");

        job.BlobReference.ShouldBeNull();
        job.FileName.ShouldBeNull();
        job.RowCount.ShouldBeNull();
        job.ErrorMessage.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
        job.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var job = ExportJob.Create(
            id,
            "Acme.PatientExport",
            "xlsx",
            """{"filter":"active"}""",
            tenantId);

        job.Id.ShouldBe(id);
        job.DefinitionName.ShouldBe("Acme.PatientExport");
        job.Format.ShouldBe("xlsx");
        job.RequestJson.ShouldBe("""{"filter":"active"}""");
        job.Status.ShouldBe(ExportJobStatus.Queued);
        job.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Complete_SetsCompletedState()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(),
            "Test",
            "xlsx",
            "{}");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        job.Complete("exports/abc.xlsx", "patients_2026-03-03.xlsx", 42, now);

        job.Status.ShouldBe(ExportJobStatus.Completed);
        job.BlobReference.ShouldBe("exports/abc.xlsx");
        job.FileName.ShouldBe("patients_2026-03-03.xlsx");
        job.RowCount.ShouldBe(42);
        job.CompletedAt.ShouldBe(now);
    }

    [Fact]
    public void MarkAsExporting_TransitionsStatus()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(),
            "Test",
            "csv",
            "{}");

        job.MarkAsExporting();

        job.Status.ShouldBe(ExportJobStatus.Exporting);
    }

    [Fact]
    public void Fail_SetsFailedState()
    {
        var job = ExportJob.Create(
            Guid.NewGuid(),
            "Test",
            "csv",
            "{}");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        job.Fail("Something went wrong", now);

        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.ErrorMessage.ShouldBe("Something went wrong");
        job.CompletedAt.ShouldBe(now);
    }
}
