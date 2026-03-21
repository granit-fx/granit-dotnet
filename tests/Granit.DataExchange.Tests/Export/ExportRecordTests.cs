using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportRecordTests
{
    // ── ExportRequest ────────────────────────────────────────────

    [Fact]
    public void ExportRequest_Constructor_SetsAllProperties()
    {
        Dictionary<string, string> filter = new() { ["name.contains"] = "John" };
        Dictionary<string, string> presets = new() { ["status"] = "active" };

        ExportRequest request = new(
            "Acme.PatientExport",
            "xlsx",
            ["Name", "Email"],
            IncludeIdForImport: true,
            Sort: "-createdAt,lastName",
            Filter: filter,
            Presets: presets,
            Search: "test");

        request.DefinitionName.ShouldBe("Acme.PatientExport");
        request.Format.ShouldBe("xlsx");
        request.SelectedFields.ShouldBe(["Name", "Email"]);
        request.IncludeIdForImport.ShouldBeTrue();
        request.Sort.ShouldBe("-createdAt,lastName");
        request.Filter.ShouldBe(filter);
        request.Presets.ShouldBe(presets);
        request.Search.ShouldBe("test");
    }

    [Fact]
    public void ExportRequest_NullOptionalFields()
    {
        ExportRequest request = new(
            "Acme.Export", "csv", null, false, null, null, null, null);

        request.SelectedFields.ShouldBeNull();
        request.Sort.ShouldBeNull();
        request.Filter.ShouldBeNull();
        request.Presets.ShouldBeNull();
        request.Search.ShouldBeNull();
    }

    // ── ExportPreset ─────────────────────────────────────────────

    [Fact]
    public void ExportPreset_Constructor_SetsAllProperties()
    {
        ExportPreset preset = new(
            "Acme.PatientExport",
            "Monthly Report",
            ["Name", "Email", "CreatedAt"],
            "csv",
            IncludeIdForImport: true);

        preset.DefinitionName.ShouldBe("Acme.PatientExport");
        preset.PresetName.ShouldBe("Monthly Report");
        preset.SelectedFields.ShouldBe(["Name", "Email", "CreatedAt"]);
        preset.Format.ShouldBe("csv");
        preset.IncludeIdForImport.ShouldBeTrue();
    }

    [Fact]
    public void ExportPreset_SameDefinitionAndPresetName()
    {
        IReadOnlyList<string> fields = ["A"];
        ExportPreset a = new("def", "preset", fields, "csv", false);
        ExportPreset b = new("def", "preset", fields, "csv", false);

        // Records with shared list reference are equal
        a.ShouldBe(b);
    }

    // ── ExportJobResult ──────────────────────────────────────────

    [Fact]
    public void ExportJobResult_Constructor_SetsAllProperties()
    {
        var jobId = Guid.NewGuid();
        ExportJobResult result = new(jobId, ExportJobStatus.Queued);

        result.JobId.ShouldBe(jobId);
        result.Status.ShouldBe(ExportJobStatus.Queued);
    }

    [Fact]
    public void ExportJobResult_CompletedStatus()
    {
        ExportJobResult result = new(Guid.NewGuid(), ExportJobStatus.Completed);

        result.Status.ShouldBe(ExportJobStatus.Completed);
    }

    // ── ExportDownload ───────────────────────────────────────────

    [Fact]
    public void ExportDownload_Constructor_SetsAllProperties()
    {
        using MemoryStream stream = new();
        ExportDownload download = new(stream, "text/csv", "export.csv");

        download.Content.ShouldBeSameAs(stream);
        download.MimeType.ShouldBe("text/csv");
        download.FileName.ShouldBe("export.csv");
    }

    // ── ExecuteExportCommand ─────────────────────────────────────

    [Fact]
    public void ExecuteExportCommand_Constructor_SetsJobId()
    {
        var jobId = Guid.NewGuid();
        ExecuteExportCommand command = new(jobId);

        command.ExportJobId.ShouldBe(jobId);
    }

    [Fact]
    public void ExecuteExportCommand_Equality()
    {
        var jobId = Guid.NewGuid();
        ExecuteExportCommand a = new(jobId);
        ExecuteExportCommand b = new(jobId);

        a.ShouldBe(b);
    }

    // ── ExportJobCompletedEvent ──────────────────────────────────

    [Fact]
    public void ExportJobCompletedEvent_Constructor_SetsAllProperties()
    {
        var jobId = Guid.NewGuid();

        ExportJobCompletedEvent evt = new(
            jobId, "Acme.Export", ExportJobStatus.Completed, "user-123", 500, null);

        evt.ExportJobId.ShouldBe(jobId);
        evt.DefinitionName.ShouldBe("Acme.Export");
        evt.Status.ShouldBe(ExportJobStatus.Completed);
        evt.UserId.ShouldBe("user-123");
        evt.RowCount.ShouldBe(500);
        evt.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void ExportJobCompletedEvent_FailedWithError()
    {
        ExportJobCompletedEvent evt = new(
            Guid.NewGuid(), "def", ExportJobStatus.Failed, "user", null, "Connection refused");

        evt.Status.ShouldBe(ExportJobStatus.Failed);
        evt.RowCount.ShouldBeNull();
        evt.ErrorMessage.ShouldBe("Connection refused");
    }
}
