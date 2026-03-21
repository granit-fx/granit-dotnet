using Granit.DataExchange.Import.Domain;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Domain;

public sealed class ImportJobTests
{
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
        job.BlobReference.ShouldBe("blob/ref");
        job.Status.ShouldBe(ImportJobStatus.Created);
        job.TenantId.ShouldBe(tenantId);
        job.MappingsJson.ShouldBeNull();
        job.ReportJson.ShouldBeNull();
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
    public void SetMappings_StoresMappingsJson()
    {
        ImportJob job = CreateJob();
        const string json = """[{"SourceColumn":"Email","TargetProperty":"Email"}]""";

        job.SetMappings(json);

        job.MappingsJson.ShouldBe(json);
    }

    [Fact]
    public void ConfirmMappings_SetsMappingsAndTransitionsToMapped()
    {
        ImportJob job = CreateJob();
        const string json = """[{"SourceColumn":"Email","TargetProperty":"Email"}]""";

        job.ConfirmMappings(json);

        job.Status.ShouldBe(ImportJobStatus.Mapped);
        job.MappingsJson.ShouldBe(json);
    }

    [Fact]
    public void MarkAsExecuting_TransitionsToExecutingStatus()
    {
        ImportJob job = CreateJob();

        job.MarkAsExecuting();

        job.Status.ShouldBe(ImportJobStatus.Executing);
    }

    [Fact]
    public void Complete_SetsStatusReportAndCompletedAt()
    {
        ImportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        const string reportJson = """{"totalRows":100}""";

        job.Complete(ImportJobStatus.Completed, reportJson, completedAt);

        job.Status.ShouldBe(ImportJobStatus.Completed);
        job.ReportJson.ShouldBe(reportJson);
        job.CompletedAt.ShouldBe(completedAt);
    }

    [Fact]
    public void Complete_WithPartiallyCompleted_SetsCorrectStatus()
    {
        ImportJob job = CreateJob();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;

        job.Complete(ImportJobStatus.PartiallyCompleted, "{}", completedAt);

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
