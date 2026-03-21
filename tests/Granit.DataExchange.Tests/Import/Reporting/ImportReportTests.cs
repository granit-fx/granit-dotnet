using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Reporting;

public sealed class ImportReportAdditionalTests
{
    [Fact]
    public void Report_WithAllFieldsSet_ReturnsCorrectValues()
    {
        ImportReport report = new()
        {
            TotalRows = 1000,
            SucceededRows = 950,
            FailedRows = 30,
            SkippedRows = 20,
            InsertedRows = 800,
            UpdatedRows = 150,
            Duration = TimeSpan.FromMinutes(2.5),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors =
            [
                new ImportRowError(5, ImportRowErrorKind.Validation, ["E1"], "Invalid email"),
                new ImportRowError(10, ImportRowErrorKind.Conversion, ["E2"], "Bad date format"),
            ],
        };

        report.TotalRows.ShouldBe(1000);
        report.SucceededRows.ShouldBe(950);
        report.FailedRows.ShouldBe(30);
        report.SkippedRows.ShouldBe(20);
        report.InsertedRows.ShouldBe(800);
        report.UpdatedRows.ShouldBe(150);
        report.Duration.ShouldBe(TimeSpan.FromMinutes(2.5));
        report.FinalStatus.ShouldBe(ImportJobStatus.PartiallyCompleted);
        report.RowErrors.Count.ShouldBe(2);
    }

    [Fact]
    public void Report_SuccessfulImport_HasNoErrors()
    {
        ImportReport report = new()
        {
            TotalRows = 100,
            SucceededRows = 100,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 100,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(5),
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        };

        report.FinalStatus.ShouldBe(ImportJobStatus.Completed);
        report.RowErrors.ShouldBeEmpty();
    }
}
