using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Reporting;

public sealed class ImportReportAdditionalTests
{
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
