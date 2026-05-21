using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Domain;

public sealed class ImportJobTests
{
    private static readonly ImportColumnMapping[] SampleMappings =
        [new("Email", "Email", MappingConfidence.Manual)];

    private static ImportReport SampleReport(ImportJobStatus status = ImportJobStatus.Completed, int totalRows = 100) => new()
    {
        TotalRows = totalRows,
        SucceededRows = totalRows,
        FailedRows = 0,
        SkippedRows = 0,
        InsertedRows = totalRows,
        UpdatedRows = 0,
        Duration = TimeSpan.Zero,
        FinalStatus = status,
        RowErrors = [],
    };

    private static ImportJob CreateJob(Guid? tenantId = null) =>
        ImportJob.Create(
            Guid.NewGuid(),
            "Acme.PatientImport",
            "Patient",
            "patients.csv",
            "text/csv",
            1024,
            "blob/patients.csv",
            tenantId);

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var job = ImportJob.Create(
            id,
            "Acme.PatientImport",
            "Patient",
            "patients.csv",
            "text/csv",
            2048,
            "blob/ref",
            tenantId);

        job.Id.ShouldBe(id);
        job.DefinitionName.ShouldBe("Acme.PatientImport");
        job.EntityTypeName.ShouldBe("Patient");
        job.OriginalFileName.ShouldBe("patients.csv");
        job.MimeType.ShouldBe("text/csv");
        job.FileSizeBytes.ShouldBe(2048);
        job.BlobReference.Value.ShouldBe("blob/ref");
        job.Status.ShouldBe(ImportJobStatus.Created);
        job.TenantId.ShouldBe(tenantId);
        job.Mappings.ShouldBeNull();
        job.Report.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutTenant_TenantIdIsNull()
    {
        ImportJob job = CreateJob();

        job.TenantId.ShouldBeNull();
    }

    [Fact]
    public void MarkAsPreviewed_TransitionsToPreviewedStatus()
    {
        ImportJob job = CreateJob();

        job.MarkAsPreviewed();

        job.Status.ShouldBe(ImportJobStatus.Previewed);
    }

    [Fact]
    public void SetMappings_StoresTypedMappings()
    {
        ImportJob job = CreateJob();

        job.SetMappings(SampleMappings);

        job.Mappings.ShouldBe(SampleMappings);
    }

    [Fact]
    public void ConfirmMappings_SetsMappingsAndTransitionsToMapped()
    {
        ImportJob job = CreateJob();
        job.MarkAsPreviewed();

        job.ConfirmMappings(SampleMappings);

        job.Status.ShouldBe(ImportJobStatus.Mapped);
        job.Mappings.ShouldBe(SampleMappings);
    }

    [Fact]
    public void MarkAsExecuting_TransitionsToExecutingStatus()
    {
        ImportJob job = CreateJob();
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);

        job.MarkAsExecuting();

        job.Status.ShouldBe(ImportJobStatus.Executing);
    }

    [Fact]
    public void Complete_SetsStatusReportAndCompletedAt()
    {
        ImportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        ImportReport report = SampleReport();
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();

        job.Complete(ImportJobStatus.Completed, report, completedAt);

        job.Status.ShouldBe(ImportJobStatus.Completed);
        job.Report.ShouldBe(report);
        job.CompletedAt.ShouldBe(completedAt);
    }

    [Fact]
    public void Complete_WithPartiallyCompleted_SetsCorrectStatus()
    {
        ImportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();

        job.Complete(ImportJobStatus.PartiallyCompleted, SampleReport(ImportJobStatus.PartiallyCompleted), completedAt);

        job.Status.ShouldBe(ImportJobStatus.PartiallyCompleted);
    }

    [Fact]
    public void Cancel_TransitionsToCancelledAndRaisesDomainEvent()
    {
        ImportJob job = CreateJob();

        job.Cancel();

        job.Status.ShouldBe(ImportJobStatus.Cancelled);
        job.DomainEvents.ShouldNotBeEmpty();
    }
}
