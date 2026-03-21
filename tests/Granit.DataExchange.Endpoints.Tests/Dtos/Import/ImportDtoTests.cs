using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Dtos.Import;

public sealed class ImportDtoTests
{
    // ── ImportJobResponse ────────────────────────────────────────

    [Fact]
    public void ImportJobResponse_FromJob_MapsAllProperties()
    {
        var job = ImportJob.Create(
            Guid.NewGuid(), "Acme.Import", "Patient",
            "patients.csv", "text/csv", 4096, "blob/ref");

        var response = ImportJobResponse.FromJob(job);

        response.Id.ShouldBe(job.Id);
        response.DefinitionName.ShouldBe("Acme.Import");
        response.OriginalFileName.ShouldBe("patients.csv");
        response.MimeType.ShouldBe("text/csv");
        response.FileSizeBytes.ShouldBe(4096);
        response.Status.ShouldBe(ImportJobStatus.Created);
        response.CompletedAt.ShouldBeNull();
    }

    // ── ImportReportResponse ─────────────────────────────────────

    [Fact]
    public void ImportReportResponse_FromReport_MapsAllProperties()
    {
        var jobId = Guid.NewGuid();
        ImportReport report = new()
        {
            TotalRows = 500,
            SucceededRows = 480,
            FailedRows = 10,
            SkippedRows = 10,
            InsertedRows = 400,
            UpdatedRows = 80,
            Duration = TimeSpan.FromSeconds(15),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors =
            [
                new ImportRowError(5, ImportRowErrorKind.Validation, ["E1"], "Bad value"),
            ],
        };

        var response = ImportReportResponse.FromReport(jobId, report);

        response.ImportJobId.ShouldBe(jobId);
        response.FinalStatus.ShouldBe(ImportJobStatus.PartiallyCompleted);
        response.TotalRows.ShouldBe(500);
        response.SucceededRows.ShouldBe(480);
        response.FailedRows.ShouldBe(10);
        response.SkippedRows.ShouldBe(10);
        response.InsertedRows.ShouldBe(400);
        response.UpdatedRows.ShouldBe(80);
        response.Duration.ShouldBe(TimeSpan.FromSeconds(15));
        response.RowErrors.Count.ShouldBe(1);
    }

    // ── ImportPreviewResponse ────────────────────────────────────

    [Fact]
    public void ImportPreviewResponse_Constructor_SetsAllProperties()
    {
        List<string> headers = ["Name", "Email"];
        List<string[]> previewRows = [["Alice", "alice@example.com"]];
        List<ImportColumnMapping> suggestions =
        [
            new("Name", "Name", MappingConfidence.Exact),
        ];
        List<ImportFieldMetadata> fieldMetadata =
        [
            new("Name", "String", "Name", null, true),
        ];

        ImportPreviewResponse response = new(headers, previewRows, suggestions, fieldMetadata);

        response.Headers.ShouldBe(["Name", "Email"]);
        response.PreviewRows.Count.ShouldBe(1);
        response.Suggestions.Count.ShouldBe(1);
        response.FieldMetadata.Count.ShouldBe(1);
    }

    // ── ConfirmMappingsRequest ───────────────────────────────────

    [Fact]
    public void ConfirmMappingsRequest_Constructor_SetsMappings()
    {
        List<ImportColumnMapping> mappings =
        [
            new("Email", "Email", MappingConfidence.Manual),
            new("Nom", "LastName", MappingConfidence.Exact),
        ];

        ConfirmMappingsRequest request = new(mappings);

        request.Mappings.Count.ShouldBe(2);
        request.Mappings[0].SourceColumn.ShouldBe("Email");
        request.Mappings[1].TargetProperty.ShouldBe("LastName");
    }
}
