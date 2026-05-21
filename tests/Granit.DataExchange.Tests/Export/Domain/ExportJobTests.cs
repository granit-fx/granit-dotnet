using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export.Domain;

public sealed class ExportJobTests
{
    private static ExportRequest SampleRequest(string format = "xlsx") =>
        new("Acme.PatientExport", format, null, false, null, null, null, null);

    private static ExportJob CreateJob(Guid? tenantId = null) =>
        ExportJob.Create(
            Guid.NewGuid(),
            "Acme.PatientExport",
            "xlsx",
            SampleRequest(),
            tenantId);

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        ExportRequest request = SampleRequest("csv");

        var job = ExportJob.Create(
            id,
            "Acme.PatientExport",
            "csv",
            request,
            tenantId);

        job.Id.ShouldBe(id);
        job.DefinitionName.ShouldBe("Acme.PatientExport");
        job.Format.ShouldBe("csv");
        job.Request.ShouldBe(request);
        job.Status.ShouldBe(ExportJobStatus.Queued);
        job.TenantId.ShouldBe(tenantId);
        job.BlobReference.ShouldBeNull();
        job.FileName.ShouldBeNull();
        job.RowCount.ShouldBeNull();
        job.ErrorMessage.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutTenant_TenantIdIsNull()
    {
        ExportJob job = CreateJob();

        job.TenantId.ShouldBeNull();
    }

    [Fact]
    public void MarkAsExporting_TransitionsToExportingStatus()
    {
        ExportJob job = CreateJob();

        job.MarkAsExporting();

        job.Status.ShouldBe(ExportJobStatus.Exporting);
    }

    [Fact]
    public void Complete_SetsAllCompletionFields()
    {
        ExportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        job.MarkAsExporting();

        job.Complete("blob/export.xlsx", "patients_2026-03-21.xlsx", 500, completedAt);

        job.Status.ShouldBe(ExportJobStatus.Completed);
        job.BlobReference!.Value.ShouldBe("blob/export.xlsx");
        job.FileName.ShouldBe("patients_2026-03-21.xlsx");
        job.RowCount.ShouldBe(500);
        job.CompletedAt.ShouldBe(completedAt);
    }

    [Fact]
    public void Fail_SetsErrorAndStatus()
    {
        ExportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        job.MarkAsExporting();

        job.Fail("Database connection lost", completedAt);

        job.Status.ShouldBe(ExportJobStatus.Failed);
        job.ErrorMessage.ShouldBe("Database connection lost");
        job.CompletedAt.ShouldBe(completedAt);
    }
}
