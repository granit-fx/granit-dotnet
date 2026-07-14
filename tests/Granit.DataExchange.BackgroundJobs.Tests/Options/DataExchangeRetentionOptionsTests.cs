using Granit.DataExchange.BackgroundJobs.Options;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests.Options;

public sealed class DataExchangeRetentionOptionsTests
{
    [Fact]
    public void SectionName_should_be_data_exchange_retention() =>
        DataExchangeRetentionOptions.SectionName.ShouldBe("DataExchange:Retention");

    [Fact]
    public void Defaults_should_match_documented_values()
    {
        var options = new DataExchangeRetentionOptions();

        options.ImportFileRetention.ShouldBe(TimeSpan.FromDays(30));
        options.ExportFileRetention.ShouldBe(TimeSpan.FromDays(7));
        options.JobRecordRetention.ShouldBe(TimeSpan.FromDays(365));
        options.StuckJobTimeout.ShouldBe(TimeSpan.FromHours(6));
        options.SweepBatchSize.ShouldBe(500);
    }
}
