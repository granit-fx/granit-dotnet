using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Reporting;

public sealed class ImportReportTests
{
    [Fact]
    public void Report_with_partial_success()
    {
        // Arrange
        ImportReport report = new()
        {
            TotalRows = 100,
            SucceededRows = 95,
            FailedRows = 5,
            SkippedRows = 0,
            InsertedRows = 95,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(2),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors =
            [
                new ImportRowError(10, ImportRowErrorKind.Validation, ["Validation:NotEmpty"], "Name is required"),
                new ImportRowError(25, ImportRowErrorKind.Conversion, ["Granit:DataExchange:InvalidFormat"], "Invalid date"),
            ],
        };

        // Assert
        report.RowErrors.Count.ShouldBe(2);
        report.FinalStatus.ShouldBe(ImportJobStatus.PartiallyCompleted);
        report.FailedRows.ShouldBe(5);
    }

    [Fact]
    public void Report_row_error_kinds_cover_all_stages() =>
        // Assert — all error kinds exist
        Enum.GetValues<ImportRowErrorKind>().Length.ShouldBe(4);
}
